using Microsoft.EntityFrameworkCore;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Exceptions;
using PizzariaLucrativa.Application.Interfaces;
using PizzariaLucrativa.Domain.Entities;
using PizzariaLucrativa.Domain.Enums;
using PizzariaLucrativa.Domain.Exceptions;
using PizzariaLucrativa.Domain.Services;

namespace PizzariaLucrativa.Application.Services;

public sealed class TechnicalSheetService : ITechnicalSheetService
{
    private readonly IAppDbContext _db;
    private readonly ITenantProvider _tenant;
    private readonly IRecalculationQueue _recalculationQueue;

    public TechnicalSheetService(
        IAppDbContext db,
        ITenantProvider tenant,
        IRecalculationQueue recalculationQueue)
    {
        _db = db;
        _tenant = tenant;
        _recalculationQueue = recalculationQueue;
    }

    public async Task<PagedResult<TechnicalSheetDto>> ListAsync(
        MarginStatus? status, PagedRequest page, CancellationToken ct = default)
    {
        var query = _db.Set<TechnicalSheet>().AsQueryable();
        if (status.HasValue)
            query = query.Where(s => s.MarginStatus == status.Value);

        var total = await _db.CountAsync(query, ct);
        var sheets = await _db.ToListAsync(
            query
                .OrderByDescending(s => s.MarginStatus)
                .ThenBy(s => s.Product!.Name)
                .ThenBy(s => s.Size!.Name)
                .Skip(page.Skip)
                .Take(page.PageSize)
                .Select(s => new
                {
                    s.Id,
                    s.ProductId,
                    ProductName = s.Product!.Name,
                    s.SizeId,
                    SizeName = s.Size!.Name,
                    s.PackagingCost,
                    s.LossPercentage,
                    s.MarginPercentage,
                    s.DirectCost,
                    s.IndirectCost,
                    s.TotalCost,
                    s.SuggestedPrice,
                    s.CardPrice,
                    s.TaxValue,
                    s.EstimatedProfit,
                    s.MarginPercentageReal,
                    s.MarginStatus
                }), ct);

        return new PagedResult<TechnicalSheetDto>(
            sheets.Select(s => new TechnicalSheetDto(
                s.Id, s.ProductId, s.ProductName, s.SizeId, s.SizeName,
                s.PackagingCost, s.LossPercentage, s.MarginPercentage,
                s.DirectCost, s.IndirectCost, s.TotalCost, s.SuggestedPrice, s.CardPrice,
                s.TaxValue, s.EstimatedProfit, s.MarginPercentageReal, s.MarginStatus)).ToList(),
            total);
    }

    public async Task<TechnicalSheetDetailDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var sheet = await FindAsync(id, ct);
        var (fees, allocation) = await PricingInputs.BuildTenantContextAsync(_db, _tenant, ct);
        var ctx = new CalculationContext(_db);
        await ctx.LoadAsync(ct);

        var input = BuildInput(sheet, fees, allocation);
        var result = SafeCalculate(input, ctx);
        var items = await MapItemsAsync(sheet, ctx, ct);

        return new TechnicalSheetDetailDto(
            sheet.Id, sheet.ProductId, sheet.Product!.Name, sheet.SizeId, sheet.Size!.Name,
            sheet.PackagingCost, sheet.LossPercentage, sheet.MarginPercentage,
            PricingInputs.Map(result), items);
    }

    public async Task<TechnicalSheetDetailDto> CreateAsync(TechnicalSheetInput input, CancellationToken ct = default)
    {
        await EnsureUniqueAsync(input.ProductId, input.SizeId, null);

        var sheet = new TechnicalSheet
        {
            TenantId = _tenant.GetTenantId(),
            ProductId = input.ProductId,
            SizeId = input.SizeId,
            PackagingCost = input.PackagingCost,
            LossPercentage = input.LossPercentage,
            MarginPercentage = input.MarginPercentage
        };
        ApplyItems(sheet, input.Items);

        _db.Add(sheet);
        await RecalculateAndSaveAsync(sheet, ct);
        await _recalculationQueue.EnqueueAsync(sheet.TenantId, ct);

        return await GetAsync(sheet.Id, ct);
    }

    public async Task<TechnicalSheetDetailDto> UpdateAsync(Guid id, TechnicalSheetInput input, CancellationToken ct = default)
    {
        var sheet = await FindAsync(id, ct);
        await EnsureUniqueAsync(input.ProductId, input.SizeId, id);

        sheet.ProductId = input.ProductId;
        sheet.SizeId = input.SizeId;
        sheet.PackagingCost = input.PackagingCost;
        sheet.LossPercentage = input.LossPercentage;
        sheet.MarginPercentage = input.MarginPercentage;
        sheet.UpdatedAt = DateTime.UtcNow;

        var existing = await _db.ToListAsync(
            _db.Set<TechnicalSheetItem>().Where(ti => ti.TechnicalSheetId == id), ct);
        _db.RemoveRange(existing);
        ApplyItems(sheet, input.Items);

        await RecalculateAndSaveAsync(sheet, ct);
        await _recalculationQueue.EnqueueAsync(sheet.TenantId, ct);

        return await GetAsync(sheet.Id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var sheet = await FindAsync(id, ct);

        var usedInCombo = await _db.Set<ComboItem>().AnyAsync(
            c => c.ProductId == sheet.ProductId && c.SizeId == sheet.SizeId);
        if (usedInCombo)
            throw new ConflictException("Ficha técnica referenciada em combo. Remova o combo antes (FT-06).");

        var items = await _db.ToListAsync(
            _db.Set<TechnicalSheetItem>().Where(ti => ti.TechnicalSheetId == id), ct);
        _db.RemoveRange(items);

        var alerts = await _db.ToListAsync(
            _db.Set<Alert>().Where(a => a.TechnicalSheetId == id), ct);
        _db.RemoveRange(alerts);

        _db.Remove(sheet);
        await _db.SaveChangesAsync(ct);
        await _recalculationQueue.EnqueueAsync(sheet.TenantId, ct);
    }

    public async Task<PricingResultDto> CalculateAsync(Guid id, CancellationToken ct = default)
    {
        var sheet = await FindAsync(id, ct);
        var (fees, allocation) = await PricingInputs.BuildTenantContextAsync(_db, _tenant, ct);
        var ctx = new CalculationContext(_db);
        await ctx.LoadAsync(ct);

        var input = BuildInput(sheet, fees, allocation);
        var result = SafeCalculate(input, ctx);

        // Persiste derivados (leitura rápida do dashboard) — docs/05 §4.
        ApplyResult(sheet, result);
        await _db.SaveChangesAsync(ct);

        return PricingInputs.Map(result);
    }

    public async Task<PricingResultDto> PreviewAsync(PricingPreviewInput input, CancellationToken ct = default)
    {
        var (fees, allocation) = await PricingInputs.BuildTenantContextAsync(_db, _tenant, ct);
        var ctx = new CalculationContext(_db);
        await ctx.LoadAsync(ct);

        var pricingInput = PricingInputs.Build(
            fees, allocation, input.Items, input.PackagingCost, input.LossPercentage, input.MarginPercentage);

        // Rótulos do breakdown vindos do catálogo carregado.
        var result = SafeCalculate(pricingInput, ctx);
        return PricingInputs.Map(RebuildBreakdown(result, pricingInput.Items, ctx));
    }

    // ---------------------------------------------------------------- internals

    private void ApplyItems(TechnicalSheet sheet, IReadOnlyList<SheetItemInput> items)
    {
        var tenantId = _tenant.GetTenantId();
        sheet.Items.Clear();
        foreach (var item in items)
        {
            sheet.Items.Add(new TechnicalSheetItem
            {
                TenantId = tenantId,
                Type = item.Type,
                IngredientId = item.Type == SheetItemType.Ingredient ? item.IngredientId : null,
                PreparationId = item.Type == SheetItemType.Preparation ? item.PreparationId : null,
                Quantity = item.Quantity,
                Unit = item.Unit
            });
        }
    }

    private SheetPricingInput BuildInput(TechnicalSheet sheet, FeeConfig fees, FixedCostAllocation allocation)
    {
        var items = sheet.Items
            .Select(ti => new SheetItemData(
                string.Empty,
                ti.Type,
                ti.IngredientId ?? Guid.Empty,
                ti.PreparationId ?? Guid.Empty,
                ti.Quantity,
                ti.Unit))
            .ToList();

        return new SheetPricingInput(
            items, sheet.PackagingCost, sheet.LossPercentage, sheet.MarginPercentage,
            fees.DefaultMarginPercentage, fees.CardFeePercentage, fees.DeliveryFeePercentage,
            allocation, fees.LossPercentageGlobal);
    }

    private static PricingResult SafeCalculate(SheetPricingInput input, ICalculationSource ctx)
    {
        try
        {
            return PricingCalculator.PriceSheet(input, ctx);
        }
        catch (DomainException ex)
        {
            throw new ValidationException("pricing", ex.Message);
        }
    }

    private static PricingResult RebuildBreakdown(
        PricingResult result, IReadOnlyList<SheetItemData> items, CalculationContext ctx)
    {
        // Substitui rótulos vazios pelos nomes reais do catálogo.
        var labels = items
            .Select(item => item.Type == SheetItemType.Ingredient
                ? ctx.ResolveLabel(SheetItemType.Ingredient, item.IngredientId)
                : ctx.ResolveLabel(SheetItemType.Preparation, item.PreparationId))
            .ToList();

        var breakdown = new List<BreakdownItem>();
        var index = 0;
        foreach (var item in result.Breakdown)
        {
            var isCatalogItem = index < labels.Count
                && item.Label == string.Empty;
            breakdown.Add(isCatalogItem ? new BreakdownItem(labels[index++], item.Cost) : item);
        }

        // Fallback: se o motor não gravou rótulos vazios, remonta na ordem.
        if (breakdown.All(b => b.Label != string.Empty))
        {
            var rebuilt = new List<BreakdownItem>();
            var itemIndex = 0;
            foreach (var b in result.Breakdown)
            {
                if (itemIndex < labels.Count && !b.Label.StartsWith("Custo indireto")
                    && !b.Label.StartsWith("Embalagem") && !b.Label.StartsWith("Perda"))
                {
                    rebuilt.Add(new BreakdownItem(labels[itemIndex], b.Cost));
                    itemIndex++;
                }
                else
                {
                    rebuilt.Add(b);
                }
            }
            breakdown = rebuilt;
        }

        return result with { Breakdown = breakdown };
    }

    private async Task RecalculateAndSaveAsync(TechnicalSheet sheet, CancellationToken ct)
    {
        var (fees, allocation) = await PricingInputs.BuildTenantContextAsync(_db, _tenant, ct);
        var ctx = new CalculationContext(_db);
        await ctx.LoadAsync(ct);

        var input = BuildInput(sheet, fees, allocation);
        var result = SafeCalculate(input, ctx);
        ApplyResult(sheet, result);
        await _db.SaveChangesAsync(ct);
    }

    private static void ApplyResult(TechnicalSheet sheet, PricingResult result)
    {
        sheet.DirectCost = result.DirectCost;
        sheet.IndirectCost = result.IndirectCost;
        sheet.TotalCost = result.TotalCost;
        sheet.SuggestedPrice = result.SuggestedPrice;
        sheet.CardPrice = result.CardPrice;
        sheet.TaxValue = result.TaxValue;
        sheet.EstimatedProfit = result.EstimatedProfit;
        sheet.MarginPercentageReal = result.MarginRealPercentage;
        sheet.MarginStatus = result.Status;

        // Snapshot de custo unitário por item (auditoria) na mesma ordem dos itens.
        if (sheet.Items.Count > 0)
        {
            var itemCosts = result.Breakdown
                .Where(b => b.Label != "Embalagem"
                            && !b.Label.StartsWith("Perda")
                            && b.Label != "Custo indireto (rateio)")
                .ToList();
            for (var i = 0; i < sheet.Items.Count && i < itemCosts.Count; i++)
            {
                var item = sheet.Items[i];
                item.ItemCost = itemCosts[i].Cost;
                item.UnitCostSnapshot = item.Quantity == 0 ? 0 : itemCosts[i].Cost / item.Quantity;
            }
        }
    }

    private async Task EnsureUniqueAsync(Guid productId, Guid sizeId, Guid? currentId)
    {
        var exists = await _db.Set<TechnicalSheet>().AnyAsync(
            s => s.ProductId == productId && s.SizeId == sizeId && (currentId == null || s.Id != currentId));
        if (exists)
            throw new ConflictException("Já existe ficha técnica para este produto neste tamanho.");
    }

    private async Task<TechnicalSheet> FindAsync(Guid id, CancellationToken ct)
    {
        var sheet = await _db.FirstOrDefaultAsync(
            _db.Set<TechnicalSheet>()
                .Where(s => s.Id == id)
                .Include(s => s.Product)
                .Include(s => s.Size), ct);
        return sheet ?? throw new NotFoundException("Ficha técnica não encontrado.");
    }

    private async Task<IReadOnlyList<TechnicalSheetItemDto>> MapItemsAsync(
        TechnicalSheet sheet, CalculationContext ctx, CancellationToken ct)
    {
        var items = await _db.ToListAsync(
            _db.Set<TechnicalSheetItem>().Where(ti => ti.TechnicalSheetId == sheet.Id), ct);

        return items.Select(ti =>
        {
            var label = ti.Type == SheetItemType.Ingredient && ti.IngredientId is Guid iid
                ? ctx.ResolveLabel(SheetItemType.Ingredient, iid)
                : ti.Type == SheetItemType.Preparation && ti.PreparationId is Guid pid
                    ? ctx.ResolveLabel(SheetItemType.Preparation, pid)
                    : "Item removido";
            return new TechnicalSheetItemDto(
                ti.Id, ti.Type, ti.IngredientId, ti.PreparationId, label,
                ti.Quantity, ti.Unit, ti.UnitCostSnapshot, ti.ItemCost);
        }).ToList();
    }
}

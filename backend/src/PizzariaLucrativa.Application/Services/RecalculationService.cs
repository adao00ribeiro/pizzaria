using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Exceptions;
using PizzariaLucrativa.Application.Interfaces;
using PizzariaLucrativa.Domain.Entities;
using PizzariaLucrativa.Domain.Enums;
using PizzariaLucrativa.Domain.Exceptions;
using PizzariaLucrativa.Domain.Services;

namespace PizzariaLucrativa.Application.Services;

/// <summary>
/// Orquestra o recálculo em cadeia disparado por mudanças de custo (ingrediente,
/// custo fixo, taxas, estoque). Executa dentro de uma transação única e é seguro
/// executar N vezes para o mesmo estado (idempotente).
/// </summary>
public sealed class RecalculationService : IRecalculationService
{
    private readonly IAppDbContext _db;
    private readonly ITenantProvider _tenant;
    private readonly ILogger<RecalculationService> _logger;

    public RecalculationService(
        IAppDbContext db,
        ITenantProvider tenant,
        ILogger<RecalculationService> logger)
    {
        _db = db;
        _tenant = tenant;
        _logger = logger;
    }

    public async Task RecalculateTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        // O worker precisa do contexto de tenant para os filtros globais valerem.
        _tenant.SetTenant(tenantId);
        try
        {
            await _db.ExecuteInTransactionAsync(async innerCt =>
            {
                await RecalculatePreparationsAsync(innerCt);
                await RecalculateSheetsAsync(innerCt);
                await RecalculateCombosAsync(innerCt);
                await SyncMarginAlertsAsync(innerCt);
                await SyncStockAlertsAsync(innerCt);
            }, ct);
        }
        finally
        {
            _tenant.Clear();
        }
    }

    // ---------------------------------------------------------------- §11 passo 2

    private async Task RecalculatePreparationsAsync(CancellationToken ct)
    {
        var preparations = await _db.ToListAsync(_db.Set<Preparation>(), ct);
        if (preparations.Count == 0)
            return;

        var items = await _db.ToListAsync(_db.Set<PreparationItem>(), ct);
        var ctx = new CalculationContext(_db);
        await ctx.LoadAsync(ct);

        foreach (var preparation in preparations)
        {
            var data = new PreparationData(
                preparation.Id,
                preparation.Name,
                preparation.YieldQuantity,
                preparation.YieldUnit,
                items.Where(i => i.PreparationId == preparation.Id)
                    .Select(i => new PreparationItemData(
                        i.Id, i.Type, i.IngredientId, i.SourcePreparationId, i.Quantity, i.Unit))
                    .ToList());

            try
            {
                var result = PricingCalculator.PreparationCost(data, ctx);
                preparation.TotalCost = result.TotalCost;
                preparation.UnitCost = result.UnitCost;
            }
            catch (DomainException ex)
            {
                // Dado inválido (ciclo/rendimento) não pode travar a cadeia inteira:
                // mantém o último custo válido e registra.
                _logger.LogWarning(
                    "Recálculo do preparo {PreparationId} ({Name}) ignorado: {Reason}",
                    preparation.Id, preparation.Name, ex.Message);
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    // ---------------------------------------------------------------- §11 passos 3–4

    private async Task RecalculateSheetsAsync(CancellationToken ct)
    {
        var sheets = await _db.ToListAsync(
            _db.Set<TechnicalSheet>().Include(s => s.Items), ct);
        if (sheets.Count == 0)
            return;

        var (fees, allocation) = await PricingInputs.BuildTenantContextAsync(_db, _tenant, ct);
        var ctx = new CalculationContext(_db);
        await ctx.LoadAsync(ct);

        foreach (var sheet in sheets)
        {
            var input = PricingInputs.Build(
                fees,
                allocation,
                sheet.Items.Select(ti => new SheetItemInput(
                    ti.Type, ti.IngredientId, ti.PreparationId, ti.Quantity, ti.Unit)).ToList(),
                sheet.PackagingCost,
                sheet.LossPercentage,
                sheet.MarginPercentage);

            try
            {
                var result = PricingCalculator.PriceSheet(input, ctx);
                ApplySheetResult(sheet, result);
            }
            catch (DomainException ex)
            {
                _logger.LogWarning(
                    "Recálculo da ficha {SheetId} ({ProductId}/{SizeId}) ignorado: {Reason}",
                    sheet.Id, sheet.ProductId, sheet.SizeId, ex.Message);
                sheet.MarginStatus = MarginStatus.NeedsAdjustment;
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    private static void ApplySheetResult(TechnicalSheet sheet, PricingResult result)
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
    }

    // ---------------------------------------------------------------- §11 passo 5

    private async Task RecalculateCombosAsync(CancellationToken ct)
    {
        var combos = await _db.ToListAsync(_db.Set<Combo>().Include(c => c.Items), ct);
        if (combos.Count == 0)
            return;

        var tenantId = _tenant.GetTenantId();
        var fees = await _db.FirstOrDefaultAsync(
                       _db.Set<FeeConfig>().Where(f => f.TenantId == tenantId), ct)
                   ?? new FeeConfig { TenantId = tenantId };

        foreach (var combo in combos)
        {
            var inputs = combo.Items
                .Select(i => new ComboItemInputDto(i.ProductId, i.SizeId, i.Quantity))
                .ToList();

            try
            {
                var resolved = await ComboCalculator.ResolveItemsAsync(_db, inputs, ct);
                var result = ComboCalculator.Calculate(
                    fees, resolved, combo.DiscountValue, combo.FixedPrice);

                combo.TotalCost = result.TotalCost;
                combo.TotalPrice = result.TotalPrice;
                combo.TaxValue = result.TaxValue;
                combo.Profit = result.Profit;
                combo.MarginPercentage = result.MarginPercentage;
                combo.MarginStatus = result.Status;
            }
            catch (ValidationException ex)
            {
                _logger.LogWarning(
                    "Recálculo do combo {ComboId} ({Name}) ignorado: {Reason}",
                    combo.Id, combo.Name, ex.Errors.FirstOrDefault().Detail ?? ex.Message);
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    // ---------------------------------------------------------------- §11 passo 6

    private async Task SyncMarginAlertsAsync(CancellationToken ct)
    {
        var sheets = await _db.ToListAsync(
            _db.Set<TechnicalSheet>()
                .Select(s => new { s.Id, s.MarginStatus, Product = s.Product!.Name, Size = s.Size!.Name }), ct);
        var openAlerts = await _db.ToListAsync(
            _db.Set<Alert>().Where(a => a.Status == AlertStatus.Open && a.Type == AlertType.MarginBelow), ct);

        foreach (var sheet in sheets.Where(s => s.MarginStatus == MarginStatus.NeedsAdjustment))
        {
            // Único alerta aberto por (tipo, item) — UNIQUE parcial aplicada no serviço.
            if (openAlerts.Any(a => a.TechnicalSheetId == sheet.Id))
                continue;

            _db.Add(new Alert
            {
                TenantId = _tenant.GetTenantId(),
                Type = AlertType.MarginBelow,
                TechnicalSheetId = sheet.Id,
                Message = $"Margem de '{sheet.Product} {sheet.Size}' ficou abaixo da meta após recálculo."
            });
        }

        // Alerta aberto cujo item voltou ao Within é encerrado automaticamente.
        foreach (var alert in openAlerts)
        {
            if (alert.TechnicalSheetId is Guid sheetId
                && sheets.All(s => s.Id != sheetId || s.MarginStatus == MarginStatus.Within))
            {
                alert.Status = AlertStatus.Resolved;
                alert.ResolvedAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    // ---------------------------------------------------------------- §11 passo 7

    private async Task SyncStockAlertsAsync(CancellationToken ct)
    {
        var ingredients = await _db.ToListAsync(
            _db.Set<Ingredient>()
                .Select(i => new { i.Id, i.Name, i.CurrentStock, i.MinimumStock, i.PurchaseUnit }), ct);
        var openAlerts = await _db.ToListAsync(
            _db.Set<Alert>().Where(a => a.Status == AlertStatus.Open && a.Type == AlertType.LowStock), ct);

        foreach (var ingredient in ingredients.Where(i => i.MinimumStock > 0 && i.CurrentStock <= i.MinimumStock))
        {
            if (openAlerts.Any(a => a.IngredientId == ingredient.Id))
                continue;

            _db.Add(new Alert
            {
                TenantId = _tenant.GetTenantId(),
                Type = AlertType.LowStock,
                IngredientId = ingredient.Id,
                Message = $"Estoque de '{ingredient.Name}' abaixo do mínimo " +
                          $"({ingredient.CurrentStock} {ingredient.PurchaseUnit} <= {ingredient.MinimumStock} {ingredient.PurchaseUnit})."
            });
        }

        foreach (var alert in openAlerts)
        {
            if (alert.IngredientId is Guid ingredientId)
            {
                var ingredient = ingredients.FirstOrDefault(i => i.Id == ingredientId);
                if (ingredient is null || ingredient.CurrentStock > ingredient.MinimumStock)
                {
                    alert.Status = AlertStatus.Resolved;
                    alert.ResolvedAt = DateTime.UtcNow;
                }
            }
        }

        await _db.SaveChangesAsync(ct);
    }
}

using Microsoft.EntityFrameworkCore;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Exceptions;
using PizzariaLucrativa.Application.Interfaces;
using PizzariaLucrativa.Domain.Entities;
using PizzariaLucrativa.Domain.Enums;
using PizzariaLucrativa.Domain.Services;

namespace PizzariaLucrativa.Application.Services;

public sealed class IngredientService : IIngredientService
{
    private readonly IAppDbContext _db;
    private readonly ITenantProvider _tenant;
    private readonly IRecalculationQueue _recalculationQueue;

    public IngredientService(IAppDbContext db, ITenantProvider tenant, IRecalculationQueue recalculationQueue)
    {
        _db = db;
        _tenant = tenant;
        _recalculationQueue = recalculationQueue;
    }

    public async Task<PagedResult<IngredientDto>> ListAsync(string? search, PagedRequest page, CancellationToken ct = default)
    {
        var query = _db.Set<Ingredient>().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(i => i.Name.ToLower().Contains(term));
        }

        var total = await _db.CountAsync(query, ct);
        var items = await _db.ToListAsync(
            query.OrderBy(i => i.Name)
                .Skip(page.Skip)
                .Take(page.PageSize), ct);

        return new PagedResult<IngredientDto>(items.Select(Map).ToList(), total);
    }

    public async Task<IngredientDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var ingredient = await FindAsync(id, ct);
        return Map(ingredient);
    }

    public async Task<IngredientDto> CreateAsync(IngredientInput input, CancellationToken ct = default)
    {
        var ingredient = new Ingredient
        {
            TenantId = _tenant.GetTenantId(),
            Name = input.Name.Trim(),
            PurchaseUnit = input.PurchaseUnit,
            PurchaseQuantity = input.PurchaseQuantity,
            PurchasePrice = input.PurchasePrice,
            LossPercentage = input.LossPercentage,
            MinimumStock = input.MinimumStock
        };
        Recalculate(ingredient);

        _db.Add(ingredient);
        await _db.SaveChangesAsync(ct);
        await _recalculationQueue.EnqueueAsync(ingredient.TenantId, ct);

        return Map(ingredient);
    }

    public async Task<IngredientDto> UpdateAsync(Guid id, IngredientInput input, CancellationToken ct = default)
    {
        var ingredient = await FindAsync(id, ct);

        var costChanged = ingredient.PurchasePrice != input.PurchasePrice
                          || ingredient.PurchaseQuantity != input.PurchaseQuantity
                          || ingredient.LossPercentage != input.LossPercentage
                          || ingredient.PurchaseUnit != input.PurchaseUnit;

        ingredient.Name = input.Name.Trim();
        ingredient.PurchaseUnit = input.PurchaseUnit;
        ingredient.PurchaseQuantity = input.PurchaseQuantity;
        ingredient.PurchasePrice = input.PurchasePrice;
        ingredient.LossPercentage = input.LossPercentage;
        ingredient.MinimumStock = input.MinimumStock;
        ingredient.UpdatedAt = DateTime.UtcNow;

        if (costChanged)
        {
            Recalculate(ingredient);
            ingredient.CostVersion++;
        }

        await _db.SaveChangesAsync(ct);

        if (costChanged)
            await _recalculationQueue.EnqueueAsync(ingredient.TenantId, ct);

        return Map(ingredient);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var ingredient = await FindAsync(id, ct);
        var usages = await UsagesAsync(id, ct);

        if (usages.Preparations.Count > 0 || usages.Sheets.Count > 0)
        {
            var where = new List<string>();
            if (usages.Preparations.Count > 0)
                where.Add("preparos: " + string.Join(", ", usages.Preparations.Select(p => p.Name)));
            if (usages.Sheets.Count > 0)
                where.Add("fichas técnicas: " + string.Join(", ", usages.Sheets.Select(s => $"{s.ProductName} {s.SizeName}")));
            throw new ConflictException(
                $"Ingrediente em uso por {string.Join("; ", where)}. Desvincule antes de excluir.");
        }

        if (await _db.Set<StockMovement>().AnyAsync(m => m.IngredientId == id, ct))
            throw new ConflictException("Ingrediente possui movimentos de estoque. O histórico impede a exclusão.");

        _db.Remove(ingredient);
        await _db.SaveChangesAsync(ct);
        await _recalculationQueue.EnqueueAsync(ingredient.TenantId, ct);
    }

    public async Task<IngredientUsagesDto> UsagesAsync(Guid id, CancellationToken ct = default)
    {
        _ = await FindAsync(id, ct);

        var preparations = await _db.ToListAsync(
            _db.Set<PreparationItem>()
                .Where(pi => pi.IngredientId == id)
                .Select(pi => new { pi.Preparation!.Id, pi.Preparation!.Name })
                .Distinct(), ct);

        var sheets = await _db.ToListAsync(
            _db.Set<TechnicalSheetItem>()
                .Where(ti => ti.IngredientId == id)
                .Select(ti => new
                {
                    ti.TechnicalSheetId,
                    ProductName = ti.TechnicalSheet!.Product!.Name,
                    SizeName = ti.TechnicalSheet!.Size!.Name
                })
                .Distinct(), ct);

        return new IngredientUsagesDto(
            preparations.Select(p => new PreparationUsageDto(p.Id, p.Name)).ToList(),
            sheets.Select(s => new SheetUsageDto(s.TechnicalSheetId, s.ProductName, s.SizeName)).ToList());
    }

    public Task<IngredientDto> PreviewCostAsync(IngredientInput input, CancellationToken ct = default)
    {
        var data = new IngredientData(
            Guid.NewGuid(), input.Name, input.PurchaseUnit, input.PurchaseQuantity, input.PurchasePrice, input.LossPercentage);
        var unitCost = PricingCalculator.IngredientUnitCost(data);
        var dto = new IngredientDto(
            Guid.Empty, input.Name, input.PurchaseUnit, input.PurchaseQuantity, input.PurchasePrice,
            input.LossPercentage, unitCost, input.MinimumStock, 0, 0);
        return Task.FromResult(dto);
    }

    private async Task<Ingredient> FindAsync(Guid id, CancellationToken ct)
    {
        var ingredient = await _db.FirstOrDefaultAsync(_db.Set<Ingredient>().Where(i => i.Id == id), ct);
        return ingredient ?? throw new NotFoundException("Ingrediente não encontrado.");
    }

    private static void Recalculate(Ingredient ingredient)
    {
        var data = new IngredientData(
            ingredient.Id, ingredient.Name, ingredient.PurchaseUnit,
            ingredient.PurchaseQuantity, ingredient.PurchasePrice, ingredient.LossPercentage);
        ingredient.UnitCost = PricingCalculator.IngredientUnitCost(data);
    }

    private static IngredientDto Map(Ingredient i) => new(
        i.Id, i.Name, i.PurchaseUnit, i.PurchaseQuantity, i.PurchasePrice,
        i.LossPercentage, i.UnitCost, i.MinimumStock, i.CurrentStock, i.CostVersion);
}

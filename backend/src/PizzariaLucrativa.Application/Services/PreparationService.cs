using Microsoft.EntityFrameworkCore;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Exceptions;
using PizzariaLucrativa.Application.Interfaces;
using PizzariaLucrativa.Domain.Entities;
using PizzariaLucrativa.Domain.Enums;
using PizzariaLucrativa.Domain.Exceptions;
using PizzariaLucrativa.Domain.Services;

namespace PizzariaLucrativa.Application.Services;

public sealed class PreparationService : IPreparationService
{
    private readonly IAppDbContext _db;
    private readonly ITenantProvider _tenant;
    private readonly IRecalculationQueue _recalculationQueue;

    public PreparationService(IAppDbContext db, ITenantProvider tenant, IRecalculationQueue recalculationQueue)
    {
        _db = db;
        _tenant = tenant;
        _recalculationQueue = recalculationQueue;
    }

    public async Task<PagedResult<PreparationListItemDto>> ListAsync(PagedRequest page, CancellationToken ct = default)
    {
        var query = _db.Set<Preparation>();
        var total = await _db.CountAsync(query, ct);
        var items = await _db.ToListAsync(
            query.OrderBy(p => p.Name).Skip(page.Skip).Take(page.PageSize), ct);

        return new PagedResult<PreparationListItemDto>(
            items.Select(p => new PreparationListItemDto(
                p.Id, p.Name, p.YieldQuantity, p.YieldUnit, p.TotalCost, p.UnitCost, p.Items.Count)).ToList(),
            total);
    }

    public async Task<PreparationDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var preparation = await FindAsync(id, ct);
        return await MapAsync(preparation, ct);
    }

    public async Task<PreparationDto> CreateAsync(PreparationInput input, CancellationToken ct = default)
    {
        var preparation = new Preparation
        {
            TenantId = _tenant.GetTenantId(),
            Name = input.Name.Trim(),
            YieldQuantity = input.YieldQuantity,
            YieldUnit = input.YieldUnit
        };
        ApplyItems(preparation, input.Items);
        await RecalculateAsync(preparation, ct);

        _db.Add(preparation);
        await _db.SaveChangesAsync(ct);
        await _recalculationQueue.EnqueueAsync(preparation.TenantId, ct);

        return await MapAsync(preparation, ct);
    }

    public async Task<PreparationDto> UpdateAsync(Guid id, PreparationInput input, CancellationToken ct = default)
    {
        var preparation = await FindAsync(id, ct);

        preparation.Name = input.Name.Trim();
        preparation.YieldQuantity = input.YieldQuantity;
        preparation.YieldUnit = input.YieldUnit;
        preparation.UpdatedAt = DateTime.UtcNow;

        var existing = await _db.ToListAsync(
            _db.Set<PreparationItem>().Where(pi => pi.PreparationId == id), ct);
        _db.RemoveRange(existing);
        ApplyItems(preparation, input.Items);

        await RecalculateAsync(preparation, ct);
        await _db.SaveChangesAsync(ct);
        await _recalculationQueue.EnqueueAsync(preparation.TenantId, ct);

        return await MapAsync(preparation, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var preparation = await FindAsync(id, ct);

        var usedAsNested = await _db.Set<PreparationItem>()
            .AnyAsync(pi => pi.SourcePreparationId == id, ct);
        var usedInSheets = await _db.Set<TechnicalSheetItem>()
            .AnyAsync(ti => ti.PreparationId == id, ct);

        if (usedAsNested || usedInSheets)
        {
            var where = new List<string>();
            if (usedAsNested) where.Add("outros preparos");
            if (usedInSheets) where.Add("fichas técnicas");
            throw new ConflictException(
                $"Preparo em uso por {string.Join(" e ", where)}. Desvincule antes de excluir.");
        }

        var items = await _db.ToListAsync(
            _db.Set<PreparationItem>().Where(pi => pi.PreparationId == id), ct);
        _db.RemoveRange(items);
        _db.Remove(preparation);
        await _db.SaveChangesAsync(ct);
        await _recalculationQueue.EnqueueAsync(preparation.TenantId, ct);
    }

    public async Task<PreparationCalculateResult> CalculateAsync(Guid id, CancellationToken ct = default)
    {
        var preparation = await FindAsync(id, ct);
        var ctx = new CalculationContext(_db);
        await ctx.LoadAsync(ct);
        return CalculateCore(preparation, ctx);
    }

    public async Task<PreparationCalculateResult> PreviewAsync(PreparationInput input, CancellationToken ct = default)
    {
        var ctx = new CalculationContext(_db);
        await ctx.LoadAsync(ct);

        // Preparos ainda não salvos: constrói o snapshot temporário com os IDs enviados
        // e resolve os itens contra o que existe no banco.
        var temporary = new PreparationData(
            Guid.NewGuid(), input.Name, input.YieldQuantity, input.YieldUnit,
            input.Items.Select((it, index) => new PreparationItemData(
                Guid.NewGuid(),
                it.Type,
                it.Type == SheetItemType.Ingredient ? it.IngredientId : null,
                it.Type == SheetItemType.Preparation ? it.SourcePreparationId : null,
                it.Quantity,
                it.Unit)).ToList());

        // Sobrepõe o snapshot do preparo atual para permitir auto-edição sem ciclo falso.
        var overlay = new OverlaySource(ctx, temporary);
        try
        {
            var result = PricingCalculator.PreparationCost(temporary, overlay, 0);
            var breakdown = new List<BreakdownItemDto>();
            foreach (var item in temporary.Items)
            {
                var label = item.Type == SheetItemType.Ingredient
                    ? overlay.Resolve(item.IngredientId!.Value)
                    : overlay.Resolve(item.SourcePreparationId!.Value);
                decimal unitCost = item.Type == SheetItemType.Ingredient
                    ? PricingCalculator.UnitCostPerUseUnit(ctx.GetIngredient(item.IngredientId!.Value)!, item.Unit)
                    : result.TotalCost / temporary.YieldQuantity *
                      UnitConverter.Factor(item.Unit, temporary.YieldUnit);
                breakdown.Add(new BreakdownItemDto(label, decimal.Round(unitCost * item.Quantity, 4, MidpointRounding.AwayFromZero)));
            }

            return new PreparationCalculateResult(result.TotalCost, result.UnitCost, breakdown);
        }
        catch (CycleDetectedException ex)
        {
            throw new ValidationException("items", ex.Message);
        }
        catch (DomainException ex)
        {
            throw new ValidationException("items", ex.Message);
        }
    }

    private async Task RecalculateAsync(Preparation preparation, CancellationToken ct)
    {
        var ctx = new CalculationContext(_db);
        await ctx.LoadAsync(ct);

        var data = new PreparationData(
            preparation.Id, preparation.Name, preparation.YieldQuantity, preparation.YieldUnit,
            preparation.Items.Select(MapItemData).ToList());

        try
        {
            // Sobrescreve o snapshot com os itens ainda não persistidos.
            var overlay = new OverlaySource(ctx, data, includeSelf: true);
            var result = PricingCalculator.PreparationCost(data, overlay);
            preparation.TotalCost = result.TotalCost;
            preparation.UnitCost = result.UnitCost;
        }
        catch (CycleDetectedException ex)
        {
            throw new ValidationException("items", ex.Message);
        }
        catch (DomainException ex)
        {
            throw new ValidationException("items", ex.Message);
        }

        await Task.CompletedTask;
    }

    private void ApplyItems(Preparation preparation, IReadOnlyList<PreparationItemInput> items)
    {
        var tenantId = _tenant.GetTenantId();
        preparation.Items.Clear();

        foreach (var item in items)
        {
            if (item.Type == SheetItemType.Ingredient && item.IngredientId is null)
                throw new ValidationException("items", "Item de ingrediente sem ingrediente selecionado.");
            if (item.Type == SheetItemType.Preparation && item.SourcePreparationId is null)
                throw new ValidationException("items", "Item de preparo sem preparo selecionado.");
            if (item.Type == SheetItemType.Preparation && item.SourcePreparationId == preparation.Id)
                throw new ValidationException("items", "Um preparo não pode referenciar a si mesmo.");

            preparation.Items.Add(new PreparationItem
            {
                TenantId = tenantId,
                IngredientId = item.Type == SheetItemType.Ingredient ? item.IngredientId : null,
                SourcePreparationId = item.Type == SheetItemType.Preparation
                    ? item.SourcePreparationId
                    : null,
                Quantity = item.Quantity,
                Unit = item.Unit
            });
        }
    }

    private PreparationCalculateResult CalculateCore(Preparation preparation, CalculationContext ctx)
    {
        var data = new PreparationData(
            preparation.Id, preparation.Name, preparation.YieldQuantity, preparation.YieldUnit,
            preparation.Items.Select(MapItemData).ToList());

        try
        {
            var result = PricingCalculator.PreparationCost(data, ctx);
            var breakdown = preparation.Items.Select(item =>
            {
                decimal unitCost;
                if (item.Type == SheetItemType.Ingredient && item.IngredientId is Guid iid)
                    unitCost = PricingCalculator.UnitCostPerUseUnit(ctx.GetIngredient(iid)!, item.Unit);
                else if (item.SourcePreparationId is Guid sid)
                {
                    var child = ctx.GetPreparation(sid)!;
                    var childCost = PricingCalculator.PreparationCost(child, ctx);
                    unitCost = childCost.UnitCost * UnitConverter.Factor(item.Unit, child.YieldUnit);
                }
                else
                {
                    unitCost = 0;
                }

                var label = item.Type == SheetItemType.Ingredient && item.IngredientId is Guid lid
                    ? ctx.ResolveLabel(SheetItemType.Ingredient, lid)
                    : ctx.ResolveLabel(SheetItemType.Preparation, item.SourcePreparationId!.Value);

                return new BreakdownItemDto(label, decimal.Round(unitCost * item.Quantity, 4, MidpointRounding.AwayFromZero));
            }).ToList();

            return new PreparationCalculateResult(result.TotalCost, result.UnitCost, breakdown);
        }
        catch (CycleDetectedException ex)
        {
            throw new ValidationException("items", ex.Message);
        }
        catch (DomainException ex)
        {
            throw new ValidationException("items", ex.Message);
        }
    }

    private static PreparationItemData MapItemData(PreparationItem item) => new(
        item.Id,
        item.Type,
        item.IngredientId,
        item.SourcePreparationId,
        item.Quantity,
        item.Unit);

    private async Task<Preparation> FindAsync(Guid id, CancellationToken ct)
    {
        var preparation = await _db.FirstOrDefaultAsync(_db.Set<Preparation>().Where(p => p.Id == id), ct);
        return preparation ?? throw new NotFoundException("Preparo não encontrado.");
    }

    private async Task<PreparationDto> MapAsync(Preparation p, CancellationToken ct)
    {
        var items = await _db.ToListAsync(
            _db.Set<PreparationItem>().Where(pi => pi.PreparationId == p.Id), ct);

        // IDs extraídos no cliente: comparar com elementos da coleção dentro do SQL
        // não é traduzível (o provider não enxerga pi.X == i.Id de uma lista externa).
        var ingredientIds = items.Where(i => i.IngredientId.HasValue)
            .Select(i => i.IngredientId!.Value).Distinct().ToArray();
        var preparationIds = items.Where(i => i.SourcePreparationId.HasValue)
            .Select(i => i.SourcePreparationId!.Value).Distinct().ToArray();

        var ingredientNames = ingredientIds.Length == 0
            ? new List<(Guid Id, string Name)>().Select(x => new { x.Id, x.Name }).ToList()
            : await _db.ToListAsync(
                _db.Set<Ingredient>()
                    .Where(i => ingredientIds.Contains(i.Id))
                    .Select(i => new { i.Id, i.Name }), ct);
        var preparationNames = preparationIds.Length == 0
            ? new List<(Guid Id, string Name)>().Select(x => new { x.Id, x.Name }).ToList()
            : await _db.ToListAsync(
                _db.Set<Preparation>()
                    .Where(x => preparationIds.Contains(x.Id))
                    .Select(x => new { x.Id, x.Name }), ct);

        var dtos = items.Select(item =>
        {
            var label = item.Type == SheetItemType.Ingredient
                ? ingredientNames.FirstOrDefault(x => x.Id == item.IngredientId)?.Name ?? "Ingrediente removido"
                : preparationNames.FirstOrDefault(x => x.Id == item.SourcePreparationId)?.Name ?? "Preparo removido";
            return new PreparationItemDto(
                item.Id, item.Type, item.IngredientId, item.SourcePreparationId,
                label, item.Quantity, item.Unit);
        }).ToList();

        return new PreparationDto(p.Id, p.Name, p.YieldQuantity, p.YieldUnit, p.TotalCost, p.UnitCost, dtos);
    }

    /// <summary>Sobrepõe um snapshot temporário sobre a fonte do banco (edição não salva).</summary>
    private sealed class OverlaySource : ICalculationSource
    {
        private readonly ICalculationSource _inner;
        private readonly PreparationData _overlay;
        private readonly bool _includeSelf;

        public OverlaySource(ICalculationSource inner, PreparationData overlay, bool includeSelf = false)
        {
            _inner = inner;
            _overlay = overlay;
            _includeSelf = includeSelf;
        }

        public IngredientData? GetIngredient(Guid id) => _inner.GetIngredient(id);

        public PreparationData? GetPreparation(Guid id)
        {
            if (_overlay.Id == id || _includeSelf && id == _overlay.Id)
                return _overlay;
            var found = _inner.GetPreparation(id);
            // Itens novos do overlay substituem os persistidos do mesmo preparo.
            return found?.Id == _overlay.Id ? _overlay : found;
        }

        public string Resolve(Guid id)
        {
            if (id == _overlay.Id)
                return _overlay.Name;
            var labelMethod = _inner as CalculationContext;
            return labelMethod?.ResolveLabel(SheetItemType.Preparation, id)
                   ?? _inner.GetPreparation(id)?.Name
                   ?? "Preparo";
        }
    }
}

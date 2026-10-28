using PizzariaLucrativa.Application.Interfaces;
using PizzariaLucrativa.Domain.Entities;
using PizzariaLucrativa.Domain.Enums;
using PizzariaLucrativa.Domain.Services;

namespace PizzariaLucrativa.Application.Services;

/// <summary>
/// Fonte de cálculo que materializa os snapshots do tenant a partir do banco
/// (uma consulta por entidade) e devolve ao motor puro do Domain.
/// </summary>
public sealed class CalculationContext : ICalculationSource
{
    private readonly IAppDbContext _db;

    private Dictionary<Guid, IngredientData>? _ingredients;
    private Dictionary<Guid, PreparationData>? _preparations;
    private IReadOnlyDictionary<Guid, string>? _labels;

    public CalculationContext(IAppDbContext db)
    {
        _db = db;
    }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        var ingredients = await _db.ToListAsync(
            _db.Set<Ingredient>().Select(i => new IngredientData(
                i.Id, i.Name, i.PurchaseUnit, i.PurchaseQuantity, i.PurchasePrice, i.LossPercentage)), ct);

        var rawItems = await _db.ToListAsync(
            _db.Set<PreparationItem>().Select(pi => new
            {
                pi.Id,
                pi.PreparationId,
                pi.IngredientId,
                pi.SourcePreparationId,
                pi.Quantity,
                pi.Unit
            }), ct);

        var preparations = await _db.ToListAsync(
            _db.Set<Preparation>().Select(p => new { p.Id, p.Name, p.YieldQuantity, p.YieldUnit }), ct);

        var byPreparation = rawItems.GroupBy(x => x.PreparationId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<PreparationItemData>)g
                    .Select(x => new PreparationItemData(
                        x.Id,
                        x.IngredientId.HasValue ? SheetItemType.Ingredient : SheetItemType.Preparation,
                        x.IngredientId, x.SourcePreparationId, x.Quantity, x.Unit))
                    .ToList());

        _preparations = preparations.ToDictionary(
            p => p.Id,
            p => new PreparationData(
                p.Id, p.Name, p.YieldQuantity, p.YieldUnit,
                byPreparation.GetValueOrDefault(p.Id) ?? Array.Empty<PreparationItemData>()));

        _ingredients = ingredients.ToDictionary(x => x.Id);

        var labels = ingredients.ToDictionary(x => x.Id, x => x.Name);
        foreach (var p in preparations)
            labels[p.Id] = p.Name;
        _labels = labels;
    }

    public IngredientData? GetIngredient(Guid id)
    {
        EnsureLoaded();
        return _ingredients!.GetValueOrDefault(id);
    }

    public void AddIngredient(IngredientData data)
    {
        EnsureLoaded();
        _ingredients![data.Id] = data;
    }

    public PreparationData? GetPreparation(Guid id)
    {
        EnsureLoaded();
        return _preparations!.GetValueOrDefault(id);
    }

    /// <summary>Rótulo legível de um item para o breakdown.</summary>
    public string ResolveLabel(SheetItemType type, Guid id)
    {
        EnsureLoaded();
        return _labels!.GetValueOrDefault(id)
               ?? (type == SheetItemType.Ingredient ? "Ingrediente removido" : "Preparo removido");
    }

    private void EnsureLoaded()
    {
        if (_ingredients is null || _preparations is null || _labels is null)
            throw new InvalidOperationException("CalculationContext.LoadAsync deve ser chamado antes do uso.");
    }
}

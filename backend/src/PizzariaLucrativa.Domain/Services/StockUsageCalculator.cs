using PizzariaLucrativa.Domain.Enums;
using PizzariaLucrativa.Domain.Exceptions;

namespace PizzariaLucrativa.Domain.Services;

/// <summary>
/// Expansão de fichas técnicas e preparos em consumo de insumos (docs/10, Sprint 2).
/// Função pura (NF-R01): recebe snapshots por <see cref="ICalculationSource"/> e
/// acumula quantidades por ingrediente já convertidas para a unidade de compra.
/// Reaproveita <see cref="UnitConverter"/> e a política de ciclos/profundidade do motor.
/// </summary>
public static class StockUsageCalculator
{
    /// <summary>
    /// Expande os itens de uma ficha técnica (opcionalmente escalados — usado nas
    /// mistas, onde cada sabor entra com sua fração) em consumo por ingrediente.
    /// </summary>
    public static Dictionary<Guid, decimal> AccumulateSheet(
        IEnumerable<SheetItemData> items, ICalculationSource source, decimal scale = 1m)
    {
        var totals = new Dictionary<Guid, decimal>();
        AccumulateSheet(items, source, totals, scale);
        return totals;
    }

    public static void AccumulateSheet(
        IEnumerable<SheetItemData> items, ICalculationSource source,
        Dictionary<Guid, decimal> totals, decimal scale = 1m)
    {
        foreach (var item in items)
        {
            var quantity = item.Quantity * scale;
            if (item.Type == SheetItemType.Ingredient)
            {
                AddIngredient(item.IngredientId, quantity, item.Unit, source, totals);
            }
            else
            {
                var preparation = source.GetPreparation(item.PreparationId)
                    ?? throw new DomainException("Preparo da ficha não encontrado.");
                AccumulatePreparation(preparation, quantity, item.Unit, source, totals);
            }
        }
    }

    /// <summary>Expande um preparo consumido em <paramref name="amount"/> <paramref name="amountUnit"/>.</summary>
    public static void AccumulatePreparation(
        PreparationData preparation, decimal amount, MeasureUnit amountUnit,
        ICalculationSource source, Dictionary<Guid, decimal> totals)
        => AccumulatePreparation(preparation, amount, amountUnit, source, totals, new List<Guid>());

    /// <summary>Acumula a quantidade de um ingrediente convertida para a unidade de compra.</summary>
    public static void AddIngredient(
        Guid ingredientId, decimal quantity, MeasureUnit unit,
        ICalculationSource source, Dictionary<Guid, decimal> totals)
    {
        var ingredient = source.GetIngredient(ingredientId)
            ?? throw new DomainException("Ingrediente da ficha não encontrado.");

        totals[ingredientId] = totals.GetValueOrDefault(ingredientId)
            + UnitConverter.Convert(quantity, unit, ingredient.PurchaseUnit);
    }

    private static void AccumulatePreparation(
        PreparationData preparation, decimal amount, MeasureUnit amountUnit,
        ICalculationSource source, Dictionary<Guid, decimal> totals, List<Guid> trail)
    {
        if (trail.Contains(preparation.Id))
        {
            var cycle = trail.Skip(trail.IndexOf(preparation.Id)).Append(preparation.Id).ToList();
            throw new CycleDetectedException(cycle);
        }

        if (trail.Count >= PricingCalculator.MaxPreparationDepth)
            throw new DomainException(
                $"Preparo '{preparation.Name}' excede o limite de " +
                $"{PricingCalculator.MaxPreparationDepth} níveis de aninhamento.");

        if (preparation.YieldQuantity <= 0)
            throw new DomainException($"Preparo '{preparation.Name}': rendimento deve ser maior que zero.");

        // fator = (quantidade convertida para a unidade de rendimento) ÷ rendimento.
        var amountInYieldUnit = UnitConverter.Convert(amount, amountUnit, preparation.YieldUnit);
        var factor = amountInYieldUnit / preparation.YieldQuantity;

        trail.Add(preparation.Id);
        try
        {
            foreach (var item in preparation.Items)
            {
                if (item.Type == SheetItemType.Ingredient && item.IngredientId is Guid ingredientId)
                {
                    AddIngredient(ingredientId, item.Quantity * factor, item.Unit, source, totals);
                }
                else if (item.SourcePreparationId is Guid childId)
                {
                    var child = source.GetPreparation(childId)
                        ?? throw new DomainException("Preparo aninhado não encontrado.");
                    AccumulatePreparation(child, item.Quantity * factor, item.Unit, source, totals, trail);
                }
            }
        }
        finally
        {
            trail.RemoveAt(trail.Count - 1);
        }
    }
}

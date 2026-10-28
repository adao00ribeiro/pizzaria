using PizzariaLucrativa.Domain.Enums;
using PizzariaLucrativa.Domain.Exceptions;

namespace PizzariaLucrativa.Domain.Services;

/// <summary>
/// Snapshot imutável de entrada para cálculo de reposição de estoque de insumo.
/// </summary>
public sealed record StockReplenishmentItemInput(
    Guid IngredientId,
    string Name,
    MeasureUnit Unit,
    decimal PurchaseQuantity,
    decimal PurchasePrice,
    decimal UnitCost,
    decimal MinimumStock,
    decimal CurrentStock);

/// <summary>
/// Resultado do cálculo de reposição para um insumo individual.
/// </summary>
public sealed record StockReplenishmentItemResult(
    Guid IngredientId,
    string Name,
    MeasureUnit Unit,
    decimal CurrentStock,
    decimal MinimumStock,
    decimal DeficitQuantity,
    int SuggestedPackagesToBuy,
    decimal SuggestedReplenishmentQuantity,
    decimal PurchaseQuantity,
    decimal PurchasePrice,
    decimal UnitCost,
    decimal EstimatedCost,
    StockReplenishmentUrgency Urgency);

/// <summary>
/// Resultado consolidado da lista de reposição com totais operacionais e financeiros.
/// </summary>
public sealed record StockReplenishmentSummaryResult(
    IReadOnlyList<StockReplenishmentItemResult> Items,
    int TotalItemsNeedingReplenishment,
    int CriticalCount,
    int LowCount,
    int WarningCount,
    decimal TotalEstimatedCost);

/// <summary>
/// Calculadora pura e determinística de reposição e compra de estoque de insumos.
/// Livre de efeitos colaterais e sem dependências de infraestrutura (Clean Architecture / SOLID).
/// </summary>
public static class StockReplenishmentCalculator
{
    /// <summary>Multiplicador de margem de segurança para nível de alerta 'Warning' (até 25% acima do mínimo).</summary>
    public const decimal WarningThresholdMultiplier = 1.25m;

    /// <summary>
    /// Calcula a sugestão de reposição para um único insumo a partir do snapshot de entrada.
    /// </summary>
    public static StockReplenishmentItemResult CalculateItem(StockReplenishmentItemInput input)
    {
        if (input is null)
            throw new DomainException("Snapshot de insumo não pode ser nulo.");

        return CalculateItem(
            input.IngredientId,
            input.Name,
            input.Unit,
            input.PurchaseQuantity,
            input.PurchasePrice,
            input.UnitCost,
            input.MinimumStock,
            input.CurrentStock);
    }

    /// <summary>
    /// Calcula a sugestão de reposição para um único insumo a partir de parâmetros primitivos.
    /// </summary>
    public static StockReplenishmentItemResult CalculateItem(
        Guid ingredientId,
        string name,
        MeasureUnit unit,
        decimal purchaseQuantity,
        decimal purchasePrice,
        decimal unitCost,
        decimal minimumStock,
        decimal currentStock)
    {
        var safePurchaseQty = Math.Max(0m, purchaseQuantity);
        var safePurchasePrice = Math.Max(0m, purchasePrice);
        var safeUnitCost = Math.Max(0m, unitCost);
        var safeMinStock = Math.Max(0m, minimumStock);

        // Déficit: quanto falta para atingir o estoque mínimo.
        // Se currentStock for negativo e safeMinStock for 0: Math.Max(0, 0 - (-X)) = X (cobre o negativo).
        var deficitQuantity = Round4(Math.Max(0m, safeMinStock - currentStock));

        var suggestedPackagesToBuy = 0;
        if (deficitQuantity > 0m)
        {
            if (safePurchaseQty > 0m)
            {
                suggestedPackagesToBuy = (int)Math.Ceiling(deficitQuantity / safePurchaseQty);
            }
            else
            {
                suggestedPackagesToBuy = (int)Math.Ceiling(deficitQuantity);
            }
        }

        var suggestedReplenishmentQuantity = safePurchaseQty > 0m
            ? Round4(suggestedPackagesToBuy * safePurchaseQty)
            : deficitQuantity;

        decimal estimatedCost;
        if (safePurchasePrice > 0m)
        {
            estimatedCost = Round2(suggestedPackagesToBuy * safePurchasePrice);
        }
        else if (safeUnitCost > 0m)
        {
            estimatedCost = Round2(suggestedReplenishmentQuantity * safeUnitCost);
        }
        else
        {
            estimatedCost = 0m;
        }

        var urgency = ResolveUrgency(currentStock, safeMinStock);

        return new StockReplenishmentItemResult(
            IngredientId: ingredientId,
            Name: name ?? string.Empty,
            Unit: unit,
            CurrentStock: Round4(currentStock),
            MinimumStock: Round4(safeMinStock),
            DeficitQuantity: deficitQuantity,
            SuggestedPackagesToBuy: suggestedPackagesToBuy,
            SuggestedReplenishmentQuantity: suggestedReplenishmentQuantity,
            PurchaseQuantity: Round4(safePurchaseQty),
            PurchasePrice: Round2(safePurchasePrice),
            UnitCost: Round4(safeUnitCost),
            EstimatedCost: estimatedCost,
            Urgency: urgency);
    }

    /// <summary>
    /// Determina o nível de urgência com base no estoque atual e estoque mínimo.
    /// </summary>
    public static StockReplenishmentUrgency ResolveUrgency(decimal currentStock, decimal minimumStock)
    {
        if (currentStock <= 0m)
            return StockReplenishmentUrgency.Critical;

        if (currentStock <= minimumStock)
            return StockReplenishmentUrgency.Low;

        if (minimumStock > 0m && currentStock <= minimumStock * WarningThresholdMultiplier)
            return StockReplenishmentUrgency.Warning;

        return StockReplenishmentUrgency.Normal;
    }

    /// <summary>
    /// Processa e consolida uma lista de snapshots de insumos.
    /// </summary>
    public static StockReplenishmentSummaryResult Consolidate(IEnumerable<StockReplenishmentItemInput> items)
    {
        if (items is null)
            return ConsolidateResults(Array.Empty<StockReplenishmentItemResult>());

        var calculated = items.Select(CalculateItem).ToList();
        return ConsolidateResults(calculated);
    }

    /// <summary>
    /// Consolida totais operacionais e financeiros a partir de itens já calculados.
    /// </summary>
    public static StockReplenishmentSummaryResult ConsolidateResults(IEnumerable<StockReplenishmentItemResult> results)
    {
        var list = (results ?? Array.Empty<StockReplenishmentItemResult>()).ToList();

        var totalNeeding = list.Count(i => i.Urgency != StockReplenishmentUrgency.Normal || i.DeficitQuantity > 0m);
        var criticalCount = list.Count(i => i.Urgency == StockReplenishmentUrgency.Critical);
        var lowCount = list.Count(i => i.Urgency == StockReplenishmentUrgency.Low);
        var warningCount = list.Count(i => i.Urgency == StockReplenishmentUrgency.Warning);
        var totalEstimatedCost = Round2(list.Sum(i => i.EstimatedCost));

        return new StockReplenishmentSummaryResult(
            Items: list,
            TotalItemsNeedingReplenishment: totalNeeding,
            CriticalCount: criticalCount,
            LowCount: lowCount,
            WarningCount: warningCount,
            TotalEstimatedCost: totalEstimatedCost);
    }

    private static decimal Round2(decimal value)
        => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static decimal Round4(decimal value)
        => decimal.Round(value, 4, MidpointRounding.AwayFromZero);
}

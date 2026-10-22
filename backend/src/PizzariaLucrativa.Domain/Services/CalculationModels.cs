using PizzariaLucrativa.Domain.Enums;

namespace PizzariaLucrativa.Domain.Services;

/// <summary>
/// Snapshot imutável dos dados usados pelo motor de cálculo. O motor é função pura
/// (NF-R01): recebe snapshots — nunca EF Core nem entidades rastreadas.
/// </summary>
public interface ICalculationSource
{
    IngredientData? GetIngredient(Guid id);
    PreparationData? GetPreparation(Guid id);
}

public sealed record IngredientData(
    Guid Id,
    string Name,
    MeasureUnit PurchaseUnit,
    decimal PurchaseQuantity,
    decimal PurchasePrice,
    decimal LossPercentage);

public sealed record PreparationItemData(
    Guid Id,
    SheetItemType Type,
    Guid? IngredientId,
    Guid? SourcePreparationId,
    decimal Quantity,
    MeasureUnit Unit);

public sealed record PreparationData(
    Guid Id,
    string Name,
    decimal YieldQuantity,
    MeasureUnit YieldUnit,
    IReadOnlyList<PreparationItemData> Items);

/// <summary>Item de ficha técnica em forma de snapshot (o motor resolve o custo).</summary>
public sealed record SheetItemData(
    string Label,
    SheetItemType Type,
    Guid IngredientId,
    Guid PreparationId,
    decimal Quantity,
    MeasureUnit Unit);

public sealed record FixedCostInput(
    decimal MonthlyValue,
    AllocationType AllocationType,
    int? ExpectedPizzasPerMonth,
    decimal? ExpectedMonthlyRevenue);

/// <summary>Resultado do rateio de custos fixos (docs/06 §4).</summary>
public sealed record FixedCostAllocation(
    decimal IndirectCostPerPizza,
    decimal FixedOverheadPercentage);

/// <summary>Entrada completa do cálculo de precificação de uma ficha (docs/06 §3–§6).</summary>
public sealed record SheetPricingInput(
    IReadOnlyList<SheetItemData> Items,
    decimal PackagingCost,
    decimal? LossPercentage,
    decimal? MarginPercentageOverride,
    decimal DefaultMarginPercentage,
    decimal CardFeePercentage,
    decimal DeliveryFeePercentage,
    FixedCostAllocation Allocation,
    decimal GlobalLossPercentage);

public sealed record BreakdownItem(string Label, decimal Cost);

/// <summary>Resultado da precificação — contrato compartilhado com a API e o frontend.</summary>
public sealed record PricingResult(
    decimal DirectCost,
    decimal IndirectCost,
    decimal TotalCost,
    decimal SuggestedPrice,
    decimal? CardPrice,
    decimal TaxValue,
    decimal EstimatedProfit,
    decimal MarginRealPercentage,
    decimal MarginTarget,
    MarginStatus Status,
    IReadOnlyList<BreakdownItem> Breakdown);

public sealed record PreparationCostResult(decimal TotalCost, decimal UnitCost);

public sealed record ComboItemInput(Guid ProductId, Guid? SizeId, int Quantity, decimal UnitCost, decimal UnitPrice);

public sealed record ComboPricingInput(
    IReadOnlyList<ComboItemInput> Items,
    decimal DiscountValue,
    decimal? FixedPrice,
    decimal CardFeePercentage,
    decimal DeliveryFeePercentage,
    decimal DefaultMarginPercentage);

public sealed record ComboResult(
    decimal TotalCost,
    decimal TotalPrice,
    decimal TaxValue,
    decimal Profit,
    decimal MarginPercentage,
    MarginStatus Status);

public sealed record MistaFlavor(string Name, decimal Cost, decimal Price);

public sealed record MistaResult(
    decimal Cost,
    decimal Price,
    decimal TaxValue,
    decimal Profit,
    decimal MarginPercentage,
    MarginStatus Status,
    IReadOnlyList<MistaFlavor> Flavors);

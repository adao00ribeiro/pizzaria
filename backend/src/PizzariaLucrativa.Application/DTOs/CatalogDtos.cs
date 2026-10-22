using System.Text.Json.Serialization;
using PizzariaLucrativa.Domain.Enums;

namespace PizzariaLucrativa.Application.DTOs;

// ---------------------------------------------------------------- Ingredientes

public sealed record IngredientDto(
    Guid Id,
    string Name,
    MeasureUnit PurchaseUnit,
    decimal PurchaseQuantity,
    decimal PurchasePrice,
    decimal LossPercentage,
    decimal UnitCost,
    decimal MinimumStock,
    decimal CurrentStock,
    int CostVersion);

public sealed record IngredientInput(
    string Name,
    MeasureUnit PurchaseUnit,
    decimal PurchaseQuantity,
    decimal PurchasePrice,
    decimal LossPercentage,
    decimal MinimumStock);

public sealed record IngredientUsagesDto(
    IReadOnlyList<PreparationUsageDto> Preparations,
    IReadOnlyList<SheetUsageDto> Sheets);

public sealed record PreparationUsageDto(Guid Id, string Name);

public sealed record SheetUsageDto(Guid Id, string ProductName, string SizeName);

// ---------------------------------------------------------------- Preparos

public sealed record PreparationDto(
    Guid Id,
    string Name,
    decimal YieldQuantity,
    MeasureUnit YieldUnit,
    decimal TotalCost,
    decimal UnitCost,
    IReadOnlyList<PreparationItemDto> Items);

public sealed record PreparationListItemDto(
    Guid Id,
    string Name,
    decimal YieldQuantity,
    MeasureUnit YieldUnit,
    decimal TotalCost,
    decimal UnitCost,
    int ItemCount);

public sealed record PreparationItemDto(
    Guid Id,
    SheetItemType Type,
    Guid? IngredientId,
    // Contrato da API (docs/07 §3): mesmo nome usado no request.
    [property: JsonPropertyName("preparationId")] Guid? SourcePreparationId,
    string Description,
    decimal Quantity,
    MeasureUnit Unit);

public sealed record PreparationInput(
    string Name,
    decimal YieldQuantity,
    MeasureUnit YieldUnit,
    IReadOnlyList<PreparationItemInput> Items);

public sealed record PreparationItemInput(
    SheetItemType Type,
    Guid? IngredientId,
    // Contrato da API (docs/07 §3): o preparo de origem vem como "preparationId".
    [property: JsonPropertyName("preparationId")] Guid? SourcePreparationId,
    decimal Quantity,
    MeasureUnit Unit);

public sealed record PreparationCalculateResult(decimal TotalCost, decimal UnitCost, IReadOnlyList<BreakdownItemDto> Breakdown);

public sealed record BreakdownItemDto(string Label, decimal Cost);

// ---------------------------------------------------------------- Custos fixos e taxas

public sealed record FixedCostDto(
    Guid Id,
    string Description,
    decimal MonthlyValue,
    AllocationType AllocationType,
    int? ExpectedPizzasPerMonth,
    decimal? ExpectedMonthlyRevenue,
    decimal CostPerPizza);

public sealed record FixedCostInput(
    string Description,
    decimal MonthlyValue,
    AllocationType AllocationType,
    int? ExpectedPizzasPerMonth,
    decimal? ExpectedMonthlyRevenue);

public sealed record FeeConfigDto(
    decimal CardFeePercentage,
    decimal DeliveryFeePercentage,
    decimal DeliveryFixedValue,
    decimal DefaultMarginPercentage,
    decimal LossPercentageGlobal);

// ---------------------------------------------------------------- Produtos e tamanhos

public sealed record ProductDto(
    Guid Id,
    string Name,
    ProductCategory Category,
    bool IsActive,
    decimal? MarkupMultiplier,
    decimal? SuggestedPrice,
    Guid? IngredientId,
    decimal? PurchasePrice = null,
    string? ImageUrl = null);

public sealed record ProductInput(
    string Name,
    ProductCategory Category,
    bool IsActive,
    Guid? IngredientId = null,
    decimal? PurchasePrice = null,
    decimal? MarkupMultiplier = null,
    string? ImageUrl = null);

public sealed record SizeDto(Guid Id, string Name, int? DiameterCm);

public sealed record SizeInput(string Name, int? DiameterCm);

// ---------------------------------------------------------------- Bebidas

public sealed record BeverageDto(
    Guid Id,
    string Name,
    decimal PurchasePrice,
    decimal MarkupMultiplier,
    decimal SuggestedPrice,
    decimal MarginPercentage,
    MarginStatus MarginStatus,
    string? ImageUrl = null);

using PizzariaLucrativa.Domain.Enums;

namespace PizzariaLucrativa.Application.DTOs;

// ---------------------------------------------------------------- Fichas técnicas

public sealed record TechnicalSheetDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    Guid SizeId,
    string SizeName,
    decimal PackagingCost,
    decimal? LossPercentage,
    decimal? MarginPercentage,
    decimal DirectCost,
    decimal IndirectCost,
    decimal TotalCost,
    decimal SuggestedPrice,
    decimal? CardPrice,
    decimal TaxValue,
    decimal EstimatedProfit,
    decimal MarginRealPercentage,
    MarginStatus MarginStatus);

public sealed record TechnicalSheetDetailDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    Guid SizeId,
    string SizeName,
    decimal PackagingCost,
    decimal? LossPercentage,
    decimal? MarginPercentage,
    PricingResultDto Result,
    IReadOnlyList<TechnicalSheetItemDto> Items);

public sealed record TechnicalSheetItemDto(
    Guid Id,
    SheetItemType Type,
    Guid? IngredientId,
    Guid? PreparationId,
    string Description,
    decimal Quantity,
    MeasureUnit Unit,
    decimal UnitCostSnapshot,
    decimal ItemCost);

public sealed record TechnicalSheetInput(
    Guid ProductId,
    Guid SizeId,
    decimal PackagingCost,
    decimal? LossPercentage,
    decimal? MarginPercentage,
    IReadOnlyList<SheetItemInput> Items);

public sealed record SheetItemInput(
    SheetItemType Type,
    Guid? IngredientId,
    Guid? PreparationId,
    decimal Quantity,
    MeasureUnit Unit);

// ---------------------------------------------------------------- Resultado de precificação

public sealed record PricingResultDto(
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
    IReadOnlyList<BreakdownItemDto> Breakdown);

/// <summary>Preview sem salvar (form ao vivo da UI).</summary>
public sealed record PricingPreviewInput(
    Guid? ProductId,
    Guid? SizeId,
    decimal PackagingCost,
    decimal? LossPercentage,
    decimal? MarginPercentage,
    IReadOnlyList<SheetItemInput> Items);

// ---------------------------------------------------------------- Mistas

public sealed record HalfHalfInput(Guid SizeId, IReadOnlyList<Guid> ProductIds);

public sealed record MistaFlavorDto(string Name, decimal Cost, decimal Price);

public sealed record MistaResultDto(
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
    IReadOnlyList<BreakdownItemDto> Breakdown,
    IReadOnlyList<MistaFlavorDto> Flavors);

// ---------------------------------------------------------------- Combos

public sealed record ComboDto(
    Guid Id,
    string Name,
    decimal DiscountValue,
    decimal? FixedPrice,
    decimal TotalCost,
    decimal TotalPrice,
    decimal TaxValue,
    decimal Profit,
    decimal MarginPercentage,
    MarginStatus Status,
    IReadOnlyList<ComboItemDto> Items);

public sealed record ComboItemDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    Guid? SizeId,
    string? SizeName,
    int Quantity);

public sealed record ComboInput(
    string Name,
    decimal DiscountValue,
    decimal? FixedPrice,
    IReadOnlyList<ComboItemInputDto> Items);

public sealed record ComboItemInputDto(Guid ProductId, Guid? SizeId, int Quantity);

public sealed record ComboResultDto(
    decimal TotalCost,
    decimal TotalPrice,
    decimal TaxValue,
    decimal Profit,
    decimal MarginPercentage,
    MarginStatus Status);

// ---------------------------------------------------------------- Dashboard

public sealed record DashboardSummaryDto(
    int TotalItems,
    int ItemsInRed,
    decimal MonthlyFixedCost,
    decimal IndirectCostPerPizza,
    decimal AverageMargin,
    int OpenAlerts);

public sealed record RankingItemDto(
    string Name,
    string Size,
    decimal Value,
    MarginStatus MarginStatus);

public sealed record MarginChartItemDto(string Name, decimal MarginPercentage);

// ---------------------------------------------------------------- Alertas

public sealed record AlertDto(
    Guid Id,
    AlertType Type,
    string Message,
    AlertStatus Status,
    DateTime CreatedAt,
    DateTime? ResolvedAt);

public sealed record AlertCountDto(int Count);

// ---------------------------------------------------------------- Estoque

public sealed record StockBalanceDto(
    Guid IngredientId,
    string Name,
    decimal CurrentStock,
    decimal MinimumStock,
    bool BelowMinimum,
    MeasureUnit Unit);

public sealed record StockMovementDto(
    Guid Id,
    string IngredientName,
    StockMovementType Type,
    decimal Quantity,
    decimal? UnitCost,
    string? Note,
    DateTime CreatedAt);

public sealed record StockMovementInput(
    Guid IngredientId,
    StockMovementType Type,
    decimal Quantity,
    decimal? UnitCost,
    string? Note);

// ---------------------------------------------------------------- Tenant

public sealed record TenantDto(Guid Id, string Name, string? Slug, DateTime CreatedAt);

public sealed record TenantUpdateInput(string? Name, string? Slug);

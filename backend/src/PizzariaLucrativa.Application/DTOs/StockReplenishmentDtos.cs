using PizzariaLucrativa.Domain.Enums;

namespace PizzariaLucrativa.Application.DTOs;

/// <summary>
/// Parâmetros opcionais de filtro para a listagem e relatório de reposição de estoque.
/// </summary>
public sealed record StockReplenishmentFilter(
    StockReplenishmentUrgency? Urgency = null,
    bool? OnlyNeedingReplenishment = null,
    string? Search = null);

/// <summary>
/// Item individual detalhado com métricas de estoque, déficit e recomendação de compra de insumo.
/// </summary>
public sealed record StockReplenishmentItemDto(
    Guid IngredientId,
    string IngredientName,
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
/// Relatório consolidado contendo os insumos analisados e os totais operacionais/financeiros de reposição.
/// </summary>
public sealed record StockReplenishmentReportDto(
    IReadOnlyList<StockReplenishmentItemDto> Items,
    int TotalItemsNeedingReplenishment,
    int CriticalCount,
    int LowCount,
    int WarningCount,
    decimal TotalEstimatedCost,
    DateTime GeneratedAt);

using PizzariaLucrativa.Application.DTOs;

namespace PizzariaLucrativa.Application.Interfaces;

/// <summary>
/// Métricas de pedidos consumidas pelos endpoints de analytics e pelas tools do assistente
/// (docs/sprints, IA-2). O tenant vem do token; nunca é parâmetro.
/// </summary>
public interface IAnalyticsService
{
    Task<OrdersByRegionDto> GetOrdersByRegionAsync(
        AnalyticsPeriodDto period, int limit = 10, CancellationToken ct = default);

    Task<OrdersSummaryDto> GetOrdersSummaryAsync(
        AnalyticsPeriodDto period, CancellationToken ct = default);

    Task<SalesHeatmapDto> GetSalesHeatmapAsync(
        AnalyticsPeriodDto period, CancellationToken ct = default);

    Task<PeriodComparisonDto> ComparePeriodsAsync(
        AnalyticsPeriodDto periodA, AnalyticsPeriodDto periodB, CancellationToken ct = default);
}

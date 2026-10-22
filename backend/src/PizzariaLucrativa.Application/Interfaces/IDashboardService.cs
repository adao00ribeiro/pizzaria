using PizzariaLucrativa.Application.DTOs;

namespace PizzariaLucrativa.Application.Interfaces;

public interface IDashboardService
{
    Task<DashboardSummaryDto> SummaryAsync(CancellationToken ct = default);
    Task<PagedResult<RankingItemDto>> RankingsAsync(string by, string? direction, PagedRequest page, CancellationToken ct = default);
    Task<TechnicalSheetDetailDto> IndividualAsync(Guid productId, Guid sizeId, CancellationToken ct = default);
    Task<IReadOnlyList<MarginChartItemDto>> MarginsChartAsync(CancellationToken ct = default);
}

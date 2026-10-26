using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PizzariaLucrativa.Api.Controllers.Shared;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Interfaces;

namespace PizzariaLucrativa.Api.Controllers;

/// <summary>
/// Métricas para o assistente e dashboards (docs/sprints, IA-2). Restrito ao owner.
/// </summary>
[Authorize(Policy = "OwnerOnly")]
[Route("api/v{version:apiversion}/analytics")]
[ApiVersion("1.0")]
public sealed class AnalyticsController : ApiControllerBase
{
    private readonly IAnalyticsService _analytics;
    private readonly IReportPeriodResolver _periodResolver;

    public AnalyticsController(IAnalyticsService analytics, IReportPeriodResolver periodResolver)
    {
        _analytics = analytics;
        _periodResolver = periodResolver;
    }

    [HttpGet("orders/by-region")]
    public Task<OrdersByRegionDto> GetOrdersByRegion(
        [FromQuery] string? period = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int limit = 10,
        CancellationToken ct = default)
    {
        var p = ResolvePeriod(period, from, to);
        if (limit < 1 || limit > 50) limit = Math.Clamp(limit, 1, 50);
        return _analytics.GetOrdersByRegionAsync(p, limit, ct);
    }

    [HttpGet("orders/summary")]
    public Task<OrdersSummaryDto> GetOrdersSummary(
        [FromQuery] string? period = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        var p = ResolvePeriod(period, from, to);
        return _analytics.GetOrdersSummaryAsync(p, ct);
    }

    [HttpGet("sales/heatmap")]
    public Task<SalesHeatmapDto> GetSalesHeatmap(
        [FromQuery] string? period = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        var p = ResolvePeriod(period, from, to);
        return _analytics.GetSalesHeatmapAsync(p, ct);
    }

    [HttpGet("compare")]
    public Task<PeriodComparisonDto> Compare(
        [FromQuery] string? periodA = null,
        [FromQuery] string? periodB = null,
        [FromQuery] DateTime? fromA = null,
        [FromQuery] DateTime? toA = null,
        [FromQuery] DateTime? fromB = null,
        [FromQuery] DateTime? toB = null,
        CancellationToken ct = default)
    {
        var pA = fromA.HasValue && toA.HasValue ? _periodResolver.ResolveRange(fromA.Value, toA.Value) : _periodResolver.Resolve(periodA);
        var pB = fromB.HasValue && toB.HasValue ? _periodResolver.ResolveRange(fromB.Value, toB.Value) : _periodResolver.Resolve(periodB ?? "mespassado");
        return _analytics.ComparePeriodsAsync(pA, pB, ct);
    }

    private AnalyticsPeriodDto ResolvePeriod(string? period, DateTime? from, DateTime? to)
    {
        if (from.HasValue && to.HasValue)
            return _periodResolver.ResolveRange(from.Value, to.Value);
        return _periodResolver.Resolve(period ?? "30dias");
    }
}

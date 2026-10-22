using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PizzariaLucrativa.Api.Controllers.Shared;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Interfaces;

namespace PizzariaLucrativa.Api.Controllers;

/// <summary>
/// Indicadores, rankings e gráfico de margens (docs/07 §10).
/// Restrito a owner: expõe custo e margem (docs/10, Sprint 1).
/// </summary>
[Authorize(Policy = "OwnerOnly")]
[ApiVersion("1.0")]
public sealed class DashboardController : ApiControllerBase
{
    private readonly IDashboardService _dashboard;

    public DashboardController(IDashboardService dashboard) => _dashboard = dashboard;

    /// <summary>Resumo: total de itens, itens no vermelho, custos e alertas abertos.</summary>
    [HttpGet("summary")]
    public Task<DashboardSummaryDto> Summary(CancellationToken ct = default)
        => _dashboard.SummaryAsync(ct);

    /// <summary>Ranking de fichas por lucro ou custo.</summary>
    [HttpGet("rankings")]
    public Task<PagedResult<RankingItemDto>> Rankings(
        [FromQuery] string by = "profit",
        [FromQuery] string? direction = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => _dashboard.RankingsAsync(by, direction, new PagedRequest(page, pageSize), ct);

    /// <summary>Detalhe de cálculo produto × tamanho (mesmo shape de /technicalsheets/{id}).</summary>
    [HttpGet("individual")]
    public Task<TechnicalSheetDetailDto> Individual(
        [FromQuery] Guid productId,
        [FromQuery] Guid sizeId,
        CancellationToken ct = default)
        => _dashboard.IndividualAsync(productId, sizeId, ct);

    /// <summary>Série para o gráfico de margens (gráfico C).</summary>
    [HttpGet("margins-chart")]
    public Task<IReadOnlyList<MarginChartItemDto>> MarginsChart(CancellationToken ct = default)
        => _dashboard.MarginsChartAsync(ct);
}

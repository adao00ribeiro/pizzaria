using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PizzariaLucrativa.Api.Controllers.Shared;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Interfaces;

namespace PizzariaLucrativa.Api.Controllers;

/// <summary>
/// Relatório de lucro real (docs/10, Sprint 6). Restrito exclusivamente ao <c>owner</c>.
/// </summary>
[Authorize(Policy = "OwnerOnly")]
[Route("api/v{version:apiversion}/reports/profit")]
[ApiVersion("1.0")]
public sealed class ReportsController : ApiControllerBase
{
    private readonly IReportService _reports;

    public ReportsController(IReportService reports) => _reports = reports;

    /// <summary>Resumo consolidado do período: faturamento, custo real, lucro bruto, margem, taxa de cartão e rateio fixo.</summary>
    [HttpGet]
    public Task<ProfitReportDto> Summary(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct = default)
        => _reports.GetProfitSummaryAsync(from, to, ct);

    /// <summary>Série diária de faturamento, custo e lucro bruto para gráfico.</summary>
    [HttpGet("by-day")]
    public Task<IReadOnlyList<ProfitByDayDto>> ByDay(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct = default)
        => _reports.GetProfitByDayAsync(from, to, ct);

    /// <summary>Ranking de produtos/sabores por lucro real no período.</summary>
    [HttpGet("by-product")]
    public Task<IReadOnlyList<ProfitByProductDto>> ByProduct(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct = default)
        => _reports.GetProfitByProductAsync(from, to, ct);

    /// <summary>Mix de vendas e lucro por canal (Balcão, Retirada, Entrega, Site público).</summary>
    [HttpGet("by-channel")]
    public Task<IReadOnlyList<ProfitByChannelDto>> ByChannel(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct = default)
        => _reports.GetProfitByChannelAsync(from, to, ct);
}

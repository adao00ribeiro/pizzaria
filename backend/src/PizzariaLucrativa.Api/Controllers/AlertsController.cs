using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PizzariaLucrativa.Api.Controllers.Shared;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Interfaces;
using PizzariaLucrativa.Domain.Enums;

namespace PizzariaLucrativa.Api.Controllers;

/// <summary>Alertas de margem e de estoque gerados pelo recálculo em cadeia (docs/07 §11).</summary>
[Authorize]
[ApiVersion("1.0")]
public sealed class AlertsController : ApiControllerBase
{
    private readonly IAlertService _alerts;

    public AlertsController(IAlertService alerts) => _alerts = alerts;

    [HttpGet]
    public Task<PagedResult<AlertDto>> List(
        [FromQuery] AlertStatus? status,
        [FromQuery] AlertType? type,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => _alerts.ListAsync(status, type, new PagedRequest(page, pageSize), ct);

    /// <summary>Contagem de alertas abertos (badge do menu).</summary>
    [HttpGet("count")]
    public Task<AlertCountDto> Count(CancellationToken ct = default)
        => _alerts.CountAsync(ct);

    /// <summary>Marca um alerta como resolvido.</summary>
    [HttpPost("{id:guid}/resolve")]
    public Task<AlertDto> Resolve(Guid id, CancellationToken ct = default)
        => _alerts.ResolveAsync(id, ct);
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PizzariaLucrativa.Api.Controllers.Shared;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Interfaces;

using PizzariaLucrativa.Domain.Enums;

namespace PizzariaLucrativa.Api.Controllers;

/// <summary>Saldos e movimentações de estoque dos insumos (docs/07 §12).</summary>
[Authorize]
[ApiVersion("1.0")]
public sealed class StockController : ApiControllerBase
{
    private readonly IStockService _stock;
    private readonly IStockReplenishmentService _replenishmentService;

    public StockController(IStockService stock, IStockReplenishmentService replenishmentService)
    {
        _stock = stock;
        _replenishmentService = replenishmentService;
    }

    /// <summary>Saldo por ingrediente; <c>belowMinimum=true</c> filtra os abaixo do mínimo.</summary>
    [HttpGet("balances")]
    public Task<PagedResult<StockBalanceDto>> Balances(
        [FromQuery] bool? belowMinimum,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => _stock.BalancesAsync(belowMinimum, new PagedRequest(page, pageSize), ct);

    [HttpPost("movements")]
    public Task<StockMovementDto> CreateMovement(StockMovementInput input, CancellationToken ct = default)
        => _stock.CreateMovementAsync(input, ct);

    [HttpGet("movements")]
    public Task<PagedResult<StockMovementDto>> Movements(
        [FromQuery] Guid? ingredientId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => _stock.MovementsAsync(ingredientId, new PagedRequest(page, pageSize), ct);

    /// <summary>Relatório de reposição e lista de compras para suprir déficit de estoque de insumos.</summary>
    [HttpGet("replenishment-report")]
    public Task<StockReplenishmentReportDto> ReplenishmentReport(
        [FromQuery] StockReplenishmentUrgency? urgency,
        [FromQuery] bool? onlyNeedingReplenishment,
        [FromQuery] string? search,
        CancellationToken ct = default)
        => _replenishmentService.GetReplenishmentReportAsync(
            new StockReplenishmentFilter(urgency, onlyNeedingReplenishment, search), ct);
}

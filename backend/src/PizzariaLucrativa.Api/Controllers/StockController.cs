using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PizzariaLucrativa.Api.Controllers.Shared;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Interfaces;

namespace PizzariaLucrativa.Api.Controllers;

/// <summary>Saldos e movimentações de estoque dos insumos (docs/07 §12).</summary>
[Authorize]
[ApiVersion("1.0")]
public sealed class StockController : ApiControllerBase
{
    private readonly IStockService _stock;

    public StockController(IStockService stock) => _stock = stock;

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
}

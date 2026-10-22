using PizzariaLucrativa.Application.DTOs;

namespace PizzariaLucrativa.Application.Interfaces;

public interface IStockService
{
    Task<PagedResult<StockBalanceDto>> BalancesAsync(bool? belowMinimum, PagedRequest page, CancellationToken ct = default);
    Task<StockMovementDto> CreateMovementAsync(StockMovementInput input, CancellationToken ct = default);
    Task<PagedResult<StockMovementDto>> MovementsAsync(Guid? ingredientId, PagedRequest page, CancellationToken ct = default);
}

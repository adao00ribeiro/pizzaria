using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Domain.Enums;

namespace PizzariaLucrativa.Application.Interfaces;

public interface IOrderService
{
    Task<OrderDto> CreateAsync(
        CreateOrderInput input,
        CancellationToken ct = default,
        string? idempotencyKey = null);

    /// <summary>Criação anônima do site público (Sprint 5): força desconto 0, valida cliente e recalcula preços.</summary>
    Task<OrderDto> CreatePublicAsync(
        CreatePublicOrderInput input,
        CancellationToken ct = default,
        string? idempotencyKey = null);

    Task<PagedResult<OrderListItemDto>> ListAsync(
        OrderStatus? status, OrderType? type, DateTime? date, string? search,
        PagedRequest page, CancellationToken ct = default);

    Task<OrderSummaryDto> GetTodaySummaryAsync(CancellationToken ct = default);

    Task<OrderDto> GetAsync(Guid id, CancellationToken ct = default);

    Task<OrderTicketDto> GetTicketAsync(Guid id, CancellationToken ct = default);

    Task<PublicMenuDto> GetCatalogAsync(CancellationToken ct = default);

    Task<PublicOrderTrackingDto> GetPublicTrackingAsync(string code, CancellationToken ct = default);

    /// <summary>Máquina de status (docs/10, Sprint 2): valida transições e timestamps.</summary>
    Task<OrderDto> ChangeStatusAsync(Guid id, OrderStatus newStatus, string? reason, CancellationToken ct = default);

    /// <summary>Despacha/conclui um pedido pronto na cozinha (Delivery → OutForDelivery; demais → Completed).</summary>
    Task<OrderDto> FinishFromKitchenAsync(Guid id, CancellationToken ct = default);

    Task<OrderDto> CancelAsync(Guid id, string? reason, CancellationToken ct = default);
}

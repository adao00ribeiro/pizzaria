using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PizzariaLucrativa.Api.Controllers.Shared;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Interfaces;
using PizzariaLucrativa.Domain.Enums;

namespace PizzariaLucrativa.Api.Controllers;

/// <summary>
/// Pedidos do balcão/telefone (docs/10, Sprints 2 e 4).
/// Acesso restrito a <c>atendente</c> e <c>owner</c>.
/// </summary>
[Authorize(Policy = "AtendenteOuOwner")]
[ApiVersion("1.0")]
public sealed class OrdersController : ApiControllerBase
{
    private readonly IOrderService _orders;

    public OrdersController(IOrderService orders) => _orders = orders;

    /// <summary>
    /// Cria um pedido com snapshot de preço/custo e código de acompanhamento.
    /// Suporta o header <c>Idempotency-Key</c> para evitar pedido duplicado em rede instável (Sprint 4).
    /// </summary>
    [HttpPost]
    public Task<OrderDto> Create(
        CreateOrderInput input,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey = null,
        CancellationToken ct = default)
        => _orders.CreateAsync(input, ct, idempotencyKey);

    /// <summary>Resumo do dia por status e por tipo de atendimento (cabeçalho da tela — Sprint 4).</summary>
    [HttpGet("today")]
    public Task<OrderSummaryDto> Today(CancellationToken ct = default)
        => _orders.GetTodaySummaryAsync(ct);

    /// <summary>Catálogo de venda (tamanhos, pizzas com preço, combos, bebidas e taxa de entrega — sem custo/margem).</summary>
    [HttpGet("catalog")]
    public Task<PublicMenuDto> Catalog(CancellationToken ct = default)
        => _orders.GetCatalogAsync(ct);

    /// <summary>Lista paginada com filtros por status, tipo, data e busca por código/cliente.</summary>
    [HttpGet]
    public Task<PagedResult<OrderListItemDto>> List(
        [FromQuery] OrderStatus? status,
        [FromQuery] OrderType? type,
        [FromQuery] DateTime? date,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => _orders.ListAsync(status, type, date, search, new PagedRequest(page, pageSize), ct);

    [HttpGet("{id:guid}")]
    public Task<OrderDto> Get(Guid id, CancellationToken ct = default)
        => _orders.GetAsync(id, ct);

    /// <summary>Ticket/comanda imprimível com itens, frações, totais e aviso "Não é documento fiscal" (Sprint 4).</summary>
    [HttpGet("{id:guid}/ticket")]
    public Task<OrderTicketDto> Ticket(Guid id, CancellationToken ct = default)
        => _orders.GetTicketAsync(id, ct);

    /// <summary>Transição genérica de status (avançar/entregar) pela mesma máquina da cozinha.</summary>
    [HttpPost("{id:guid}/status")]
    public Task<OrderDto> ChangeStatus(Guid id, ChangeOrderStatusInput input, CancellationToken ct = default)
        => _orders.ChangeStatusAsync(id, input.Status, input.Reason, ct);

    /// <summary>Cancela o pedido (motivo obrigatório); estoque é estornado se já baixado.</summary>
    [HttpPost("{id:guid}/cancel")]
    public Task<OrderDto> Cancel(Guid id, CancelOrderInput input, CancellationToken ct = default)
        => _orders.CancelAsync(id, input.Reason, ct);
}

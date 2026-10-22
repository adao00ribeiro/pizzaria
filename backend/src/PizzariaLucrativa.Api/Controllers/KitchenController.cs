using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PizzariaLucrativa.Api.Controllers.Shared;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Interfaces;
using PizzariaLucrativa.Domain.Enums;

namespace PizzariaLucrativa.Api.Controllers;

/// <summary>
/// Painel da produção (docs/10, Sprint 2/3). Acesso restrito a <c>pizzaiolo</c> e <c>owner</c>.
/// Em 2026-10 a Sprint 2 entrega a fila e a máquina de status; a Sprint 3 adiciona o
/// frontend com polling e o enriquecimento visual.
/// </summary>
[Authorize(Policy = "ProducaoOuOwner")]
[ApiVersion("1.0")]
public sealed class KitchenController : ApiControllerBase
{
    private readonly IKitchenService _kitchen;
    private readonly IOrderService _orders;

    public KitchenController(IKitchenService kitchen, IOrderService orders)
    {
        _kitchen = kitchen;
        _orders = orders;
    }

    /// <summary>Fila ativa (Recebido → Em preparo → Pronto) com itens, receita resumida e contagens.</summary>
    [HttpGet("queue")]
    public Task<KitchenQueueDto> Queue(CancellationToken ct = default)
        => _kitchen.QueueAsync(ct);

    /// <summary>Move o pedido para "Em preparo".</summary>
    [HttpPost("queue/{id:guid}/start")]
    public Task<OrderDto> Start(Guid id, CancellationToken ct = default)
        => _orders.ChangeStatusAsync(id, OrderStatus.InPreparation, null, ct);

    /// <summary>Move o pedido para "Pronto" — dispara a baixa de estoque (idempotente).</summary>
    [HttpPost("queue/{id:guid}/ready")]
    public Task<OrderDto> Ready(Guid id, CancellationToken ct = default)
        => _orders.ChangeStatusAsync(id, OrderStatus.Ready, null, ct);

    /// <summary>Despacha/conclui um pedido pronto (Delivery → OutForDelivery; demais → Completed), retirando-o da fila.</summary>
    [HttpPost("queue/{id:guid}/finish")]
    public Task<OrderDto> Finish(Guid id, CancellationToken ct = default)
        => _orders.FinishFromKitchenAsync(id, ct);

    /// <summary>Cancela um pedido a partir do painel da produção (motivo obrigatório; estorna se já baixado).</summary>
    [HttpPost("queue/{id:guid}/cancel")]
    public Task<OrderDto> Cancel(Guid id, CancelOrderInput input, CancellationToken ct = default)
        => _orders.CancelAsync(id, input.Reason, ct);
}

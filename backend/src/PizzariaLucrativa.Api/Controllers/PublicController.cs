using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PizzariaLucrativa.Api.Controllers.Shared;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Interfaces;

namespace PizzariaLucrativa.Api.Controllers;

/// <summary>
/// Endpoints públicos do site da pizzaria (docs/10, Sprint 1 e Sprint 5).
/// O tenant é resolvido pelo segmento {slug} da rota no <c>TenantContextMiddleware</c>;
/// slug inválido já responde 404 antes de chegar aqui.
/// </summary>
[AllowAnonymous]
[Route("api/v{version:apiversion}/public/{slug}")]
[ApiVersion("1.0")]
public sealed class PublicController : ApiControllerBase
{
    private readonly IOrderService _orders;

    public PublicController(IOrderService orders) => _orders = orders;

    /// <summary>
    /// Cardápio público completo (tamanhos, pizzas precificadas por tamanho, combos e bebidas),
    /// sem qualquer informação de custo ou margem (Sprint 5).
    /// </summary>
    [HttpGet("menu")]
    public Task<PublicMenuDto> Menu(CancellationToken ct = default)
        => _orders.GetCatalogAsync(ct);

    /// <summary>
    /// Checkout anônimo do site público (Sprint 5): recalcula preços/custos no servidor
    /// (ignora qualquer valor adulterado vindo do cliente) e aplica rate limit de 10 req/min por IP.
    /// </summary>
    [HttpPost("orders")]
    [EnableRateLimiting("public-orders")]
    public Task<OrderDto> CreateOrder(
        CreatePublicOrderInput input,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey = null,
        CancellationToken ct = default)
        => _orders.CreatePublicAsync(input, ct, idempotencyKey);

    /// <summary>
    /// Acompanhamento público do pedido pelo código de 6 caracteres (Sprint 5).
    /// Devolve apenas status, timestamps e itens (sem telefone/endereço do cliente).
    /// </summary>
    [HttpGet("orders/{code}")]
    [EnableRateLimiting("public-tracking")]
    public Task<PublicOrderTrackingDto> TrackOrder(string code, CancellationToken ct = default)
        => _orders.GetPublicTrackingAsync(code, ct);
}

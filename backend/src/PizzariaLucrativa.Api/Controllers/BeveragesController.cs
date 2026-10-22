using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PizzariaLucrativa.Api.Controllers.Shared;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Interfaces;

namespace PizzariaLucrativa.Api.Controllers;

/// <summary>Bebidas com preço por markup (docs/06 §10 — docs/07 §9).</summary>
[Authorize]
[ApiVersion("1.0")]
public sealed class BeveragesController : ApiControllerBase
{
    private readonly IBeverageService _beverages;

    public BeveragesController(IBeverageService beverages) => _beverages = beverages;

    [HttpGet]
    public Task<IReadOnlyList<BeverageDto>> List(CancellationToken ct = default)
        => _beverages.ListAsync(ct);

    /// <summary>Atualiza o preço de compra e/ou multiplicador de markup da bebida.</summary>
    [HttpPut("{id:guid}/markup")]
    public Task<BeverageDto> UpdateMarkup(Guid id, MarkupInput input, CancellationToken ct = default)
        => _beverages.UpdateMarkupAsync(id, input.MarkupMultiplier, input.PurchasePrice, ct);

    public sealed record MarkupInput(decimal MarkupMultiplier, decimal? PurchasePrice = null);
}

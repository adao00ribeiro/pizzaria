using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PizzariaLucrativa.Api.Controllers.Shared;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Interfaces;
using PizzariaLucrativa.Domain.Enums;

namespace PizzariaLucrativa.Api.Controllers;

/// <summary>Produtos do cardápio: sabores de pizza, bebidas e demais itens (docs/07 §5).</summary>
[Authorize]
[ApiVersion("1.0")]
public sealed class ProductsController : ApiControllerBase
{
    private readonly IProductService _products;

    public ProductsController(IProductService products) => _products = products;

    [HttpGet]
    public Task<IReadOnlyList<ProductDto>> List(
        [FromQuery] ProductCategory? category,
        CancellationToken ct = default)
        => _products.ListAsync(category, ct);

    [HttpPost]
    public Task<ProductDto> Create(ProductInput input, CancellationToken ct = default)
        => _products.CreateAsync(input, ct);

    [HttpPut("{id:guid}")]
    public Task<ProductDto> Update(Guid id, ProductInput input, CancellationToken ct = default)
        => _products.UpdateAsync(id, input, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        await _products.DeleteAsync(id, ct);
        return NoContent();
    }
}

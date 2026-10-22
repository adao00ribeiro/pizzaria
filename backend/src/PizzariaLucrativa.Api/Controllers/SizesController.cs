using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PizzariaLucrativa.Api.Controllers.Shared;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Interfaces;

namespace PizzariaLucrativa.Api.Controllers;

/// <summary>Tamanhos de pizza (docs/07 §5).</summary>
[Authorize]
[ApiVersion("1.0")]
public sealed class SizesController : ApiControllerBase
{
    private readonly ISizeService _sizes;

    public SizesController(ISizeService sizes) => _sizes = sizes;

    [HttpGet]
    public Task<IReadOnlyList<SizeDto>> List(CancellationToken ct = default)
        => _sizes.ListAsync(ct);

    [HttpPost]
    public Task<SizeDto> Create(SizeInput input, CancellationToken ct = default)
        => _sizes.CreateAsync(input, ct);

    [HttpPut("{id:guid}")]
    public Task<SizeDto> Update(Guid id, SizeInput input, CancellationToken ct = default)
        => _sizes.UpdateAsync(id, input, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        await _sizes.DeleteAsync(id, ct);
        return NoContent();
    }
}

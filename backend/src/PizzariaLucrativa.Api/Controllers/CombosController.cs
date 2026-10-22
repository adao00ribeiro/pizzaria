using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PizzariaLucrativa.Api.Controllers.Shared;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Interfaces;

namespace PizzariaLucrativa.Api.Controllers;

/// <summary>Combos com desconto e preço derivado (docs/07 §8).</summary>
[Authorize]
[ApiVersion("1.0")]
public sealed class CombosController : ApiControllerBase
{
    private readonly IComboService _combos;

    public CombosController(IComboService combos) => _combos = combos;

    [HttpGet]
    public Task<PagedResult<ComboDto>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => _combos.ListAsync(new PagedRequest(page, pageSize), ct);

    [HttpGet("{id:guid}")]
    public Task<ComboDto> Get(Guid id, CancellationToken ct = default)
        => _combos.GetAsync(id, ct);

    [HttpPost]
    public Task<ComboDto> Create(ComboInput input, CancellationToken ct = default)
        => _combos.CreateAsync(input, ct);

    [HttpPut("{id:guid}")]
    public Task<ComboDto> Update(Guid id, ComboInput input, CancellationToken ct = default)
        => _combos.UpdateAsync(id, input, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        await _combos.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>Recalcula os derivados do combo persistido (custo, preço, margem, status).</summary>
    [HttpPost("{id:guid}/calculate")]
    public Task<ComboDto> Calculate(Guid id, CancellationToken ct = default)
        => _combos.CalculateAsync(id, ct);
}

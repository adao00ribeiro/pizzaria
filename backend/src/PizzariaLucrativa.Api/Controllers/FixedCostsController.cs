using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PizzariaLucrativa.Api.Controllers.Shared;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Interfaces;

namespace PizzariaLucrativa.Api.Controllers;

/// <summary>Custos fixos mensais com rateio (docs/07 §4).</summary>
[Authorize]
[ApiVersion("1.0")]
public sealed class FixedCostsController : ApiControllerBase
{
    private readonly IFixedCostService _fixedCosts;

    public FixedCostsController(IFixedCostService fixedCosts) => _fixedCosts = fixedCosts;

    [HttpGet]
    public Task<IReadOnlyList<FixedCostDto>> List(CancellationToken ct = default)
        => _fixedCosts.ListAsync(ct);

    [HttpPost]
    public Task<FixedCostDto> Create(FixedCostInput input, CancellationToken ct = default)
        => _fixedCosts.CreateAsync(input, ct);

    [HttpPut("{id:guid}")]
    public Task<FixedCostDto> Update(Guid id, FixedCostInput input, CancellationToken ct = default)
        => _fixedCosts.UpdateAsync(id, input, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        await _fixedCosts.DeleteAsync(id, ct);
        return NoContent();
    }
}

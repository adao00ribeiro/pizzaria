using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PizzariaLucrativa.Api.Controllers.Shared;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Interfaces;

namespace PizzariaLucrativa.Api.Controllers;

/// <summary>
/// CRUD do catálogo de bairros/regiões (docs/sprints, IA-2). Restrito ao owner.
/// </summary>
[Authorize(Policy = "OwnerOnly")]
[Route("api/v{version:apiversion}/neighborhoods")]
[ApiVersion("1.0")]
public sealed class NeighborhoodsController : ApiControllerBase
{
    private readonly INeighborhoodService _neighborhoods;

    public NeighborhoodsController(INeighborhoodService neighborhoods) => _neighborhoods = neighborhoods;

    [HttpGet]
    public Task<IReadOnlyList<NeighborhoodDto>> Get([FromQuery] bool? activeOnly = null, CancellationToken ct = default)
        => _neighborhoods.ListAsync(activeOnly, ct);

    [HttpGet("{id:guid}")]
    public Task<NeighborhoodDto> Get(Guid id, CancellationToken ct = default)
        => _neighborhoods.GetAsync(id, ct);

    [HttpPost]
    public Task<NeighborhoodDto> Create([FromBody] NeighborhoodInputDto input, CancellationToken ct = default)
        => _neighborhoods.CreateAsync(input, ct);

    [HttpPut("{id:guid}")]
    public Task<NeighborhoodDto> Update(Guid id, [FromBody] NeighborhoodInputDto input, CancellationToken ct = default)
        => _neighborhoods.UpdateAsync(id, input, ct);

    [HttpDelete("{id:guid}")]
    public Task Delete(Guid id, CancellationToken ct = default)
        => _neighborhoods.DeleteAsync(id, ct);
}

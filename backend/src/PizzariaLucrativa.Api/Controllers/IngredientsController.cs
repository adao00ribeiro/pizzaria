using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PizzariaLucrativa.Api.Controllers.Shared;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Interfaces;

namespace PizzariaLucrativa.Api.Controllers;

/// <summary>Insumos comprados a granel — custo unitário derivado (docs/07 §2).</summary>
[Authorize]
[ApiVersion("1.0")]
public sealed class IngredientsController : ApiControllerBase
{
    private readonly IIngredientService _ingredients;

    public IngredientsController(IIngredientService ingredients) => _ingredients = ingredients;

    [HttpGet]
    public Task<PagedResult<IngredientDto>> List(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => _ingredients.ListAsync(search, new PagedRequest(page, pageSize), ct);

    [HttpGet("{id:guid}")]
    public Task<IngredientDto> Get(Guid id, CancellationToken ct = default)
        => _ingredients.GetAsync(id, ct);

    /// <summary>Preparos e fichas técnicas que usam este insumo (RF-01/RF-02/RF-03).</summary>
    [HttpGet("{id:guid}/usages")]
    public Task<IngredientUsagesDto> Usages(Guid id, CancellationToken ct = default)
        => _ingredients.UsagesAsync(id, ct);

    [HttpPost]
    public Task<IngredientDto> Create(IngredientInput input, CancellationToken ct = default)
        => _ingredients.CreateAsync(input, ct);

    /// <summary>Prévia do custo unitário sem salvar (form ao vivo da UI).</summary>
    [HttpPost("preview-cost")]
    public Task<IngredientDto> PreviewCost(IngredientInput input, CancellationToken ct = default)
        => _ingredients.PreviewCostAsync(input, ct);

    [HttpPut("{id:guid}")]
    public Task<IngredientDto> Update(Guid id, IngredientInput input, CancellationToken ct = default)
        => _ingredients.UpdateAsync(id, input, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        await _ingredients.DeleteAsync(id, ct);
        return NoContent();
    }
}

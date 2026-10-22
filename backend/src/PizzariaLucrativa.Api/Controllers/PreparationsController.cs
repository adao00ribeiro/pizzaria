using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PizzariaLucrativa.Api.Controllers.Shared;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Interfaces;

namespace PizzariaLucrativa.Api.Controllers;

/// <summary>Receitas base (massa, molho...) com custo derivado dos itens (docs/07 §3).</summary>
[Authorize]
[ApiVersion("1.0")]
public sealed class PreparationsController : ApiControllerBase
{
    private readonly IPreparationService _preparations;

    public PreparationsController(IPreparationService preparations) => _preparations = preparations;

    [HttpGet]
    public Task<PagedResult<PreparationListItemDto>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => _preparations.ListAsync(new PagedRequest(page, pageSize), ct);

    [HttpGet("{id:guid}")]
    public Task<PreparationDto> Get(Guid id, CancellationToken ct = default)
        => _preparations.GetAsync(id, ct);

    [HttpPost]
    public Task<PreparationDto> Create(PreparationInput input, CancellationToken ct = default)
        => _preparations.CreateAsync(input, ct);

    [HttpPut("{id:guid}")]
    public Task<PreparationDto> Update(Guid id, PreparationInput input, CancellationToken ct = default)
        => _preparations.UpdateAsync(id, input, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        await _preparations.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>Recalcula o preparo persistido e devolve custo total/unitário.</summary>
    [HttpPost("{id:guid}/calculate")]
    public Task<PreparationCalculateResult> Calculate(Guid id, CancellationToken ct = default)
        => _preparations.CalculateAsync(id, ct);

    /// <summary>Calcula um preparo rascunho sem salvar (form ao vivo da UI).</summary>
    [HttpPost("calculate-preview")]
    public Task<PreparationCalculateResult> CalculatePreview(PreparationInput input, CancellationToken ct = default)
        => _preparations.PreviewAsync(input, ct);
}

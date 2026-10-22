using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PizzariaLucrativa.Api.Controllers.Shared;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Interfaces;
using PizzariaLucrativa.Domain.Enums;

namespace PizzariaLucrativa.Api.Controllers;

/// <summary>Fichas técnicas: produto × tamanho com itens e preço derivado (docs/07 §6).</summary>
[Authorize]
[ApiVersion("1.0")]
public sealed class TechnicalSheetsController : ApiControllerBase
{
    private readonly ITechnicalSheetService _sheets;

    public TechnicalSheetsController(ITechnicalSheetService sheets) => _sheets = sheets;

    [HttpGet]
    public Task<PagedResult<TechnicalSheetDto>> List(
        [FromQuery] MarginStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => _sheets.ListAsync(status, new PagedRequest(page, pageSize), ct);

    [HttpGet("{id:guid}")]
    public Task<TechnicalSheetDetailDto> Get(Guid id, CancellationToken ct = default)
        => _sheets.GetAsync(id, ct);

    [HttpPost]
    public Task<TechnicalSheetDetailDto> Create(TechnicalSheetInput input, CancellationToken ct = default)
        => _sheets.CreateAsync(input, ct);

    [HttpPut("{id:guid}")]
    public Task<TechnicalSheetDetailDto> Update(Guid id, TechnicalSheetInput input, CancellationToken ct = default)
        => _sheets.UpdateAsync(id, input, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        await _sheets.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>Força o cálculo completo e devolve o resultado (custo, preço, margem, status).</summary>
    [HttpPost("{id:guid}/calculate")]
    public Task<PricingResultDto> Calculate(Guid id, CancellationToken ct = default)
        => _sheets.CalculateAsync(id, ct);
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PizzariaLucrativa.Api.Controllers.Shared;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Interfaces;

namespace PizzariaLucrativa.Api.Controllers;

/// <summary>
/// Precificação sem persistência: preview de ficha, pizzas mistas e prévia de combo
/// (docs/07 §6/§7/§8 — o form reativo da UI bate aqui).
/// Restrito a owner: expõe custo e margem (docs/10, Sprint 1).
/// </summary>
[Authorize(Policy = "OwnerOnly")]
[ApiVersion("1.0")]
public sealed class PricingController : ApiControllerBase
{
    private readonly ITechnicalSheetService _sheets;
    private readonly IPricingService _pricing;

    public PricingController(ITechnicalSheetService sheets, IPricingService pricing)
    {
        _sheets = sheets;
        _pricing = pricing;
    }

    /// <summary>Calcula uma ficha sem salvar (form ao vivo da UI).</summary>
    [HttpPost("preview")]
    public Task<PricingResultDto> Preview(PricingPreviewInput input, CancellationToken ct = default)
        => _sheets.PreviewAsync(input, ct);

    /// <summary>Pizza meio a meio (2 sabores) — calculada on-demand (docs/06 §8).</summary>
    [HttpPost("half-half")]
    public Task<MistaResultDto> HalfHalf(HalfHalfInput input, CancellationToken ct = default)
        => _pricing.MistaAsync(input, expectedFlavors: 2, ct);

    /// <summary>Pizza de 4 sabores — calculada on-demand (docs/06 §8).</summary>
    [HttpPost("four-flavors")]
    public Task<MistaResultDto> FourFlavors(HalfHalfInput input, CancellationToken ct = default)
        => _pricing.MistaAsync(input, expectedFlavors: 4, ct);

    /// <summary>Prévia de combo sem salvar (docs/07 §8).</summary>
    [HttpPost("combos/preview")]
    public Task<ComboResultDto> ComboPreview(ComboInput input, CancellationToken ct = default)
        => _pricing.ComboPreviewAsync(input, ct);
}

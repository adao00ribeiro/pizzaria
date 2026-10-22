using PizzariaLucrativa.Application.DTOs;

namespace PizzariaLucrativa.Application.Interfaces;

public interface IPricingService
{
    /// <summary>Pizza mista (2 ou 4 sabores) — calculada on-demand (docs/06 §8).</summary>
    Task<MistaResultDto> MistaAsync(HalfHalfInput input, int expectedFlavors, CancellationToken ct = default);

    /// <summary>Prévia de combo sem salvar (docs/07 §8 — POST /pricing/combos/preview).</summary>
    Task<ComboResultDto> ComboPreviewAsync(ComboInput input, CancellationToken ct = default);
}

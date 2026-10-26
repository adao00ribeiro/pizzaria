using PizzariaLucrativa.Application.DTOs;

namespace PizzariaLucrativa.Application.Interfaces;

/// <summary>
/// Contabilização de tokens/custo e aplicação do orçamento mensal por tenant
/// (docs/sprints, IA-3). O bloqueio é verificado antes de chamar o provedor.
/// </summary>
public interface IAiUsageService
{
    Task<AiUsageDto> GetUsageAsync(CancellationToken ct = default);

    /// <summary>True quando o custo estimado ainda cabe no teto mensal configurado.</summary>
    Task<bool> CanSpendAsync(decimal estimatedCostUsd, CancellationToken ct = default);

    Task RecordAsync(AiUsageEntryDto entry, CancellationToken ct = default);
}

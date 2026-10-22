using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Domain.Enums;

namespace PizzariaLucrativa.Application.Interfaces;

/// <summary>CRUD de fichas técnicas + cálculo (docs/07 §6).</summary>
public interface ITechnicalSheetService
{
    Task<PagedResult<TechnicalSheetDto>> ListAsync(MarginStatus? status, PagedRequest page, CancellationToken ct = default);
    Task<TechnicalSheetDetailDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<TechnicalSheetDetailDto> CreateAsync(TechnicalSheetInput input, CancellationToken ct = default);
    Task<TechnicalSheetDetailDto> UpdateAsync(Guid id, TechnicalSheetInput input, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<PricingResultDto> CalculateAsync(Guid id, CancellationToken ct = default);
    Task<PricingResultDto> PreviewAsync(PricingPreviewInput input, CancellationToken ct = default);
}

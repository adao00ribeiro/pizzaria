using PizzariaLucrativa.Application.DTOs;

namespace PizzariaLucrativa.Application.Interfaces;

public interface IPreparationService
{
    Task<PagedResult<PreparationListItemDto>> ListAsync(PagedRequest page, CancellationToken ct = default);
    Task<PreparationDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<PreparationDto> CreateAsync(PreparationInput input, CancellationToken ct = default);
    Task<PreparationDto> UpdateAsync(Guid id, PreparationInput input, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<PreparationCalculateResult> CalculateAsync(Guid id, CancellationToken ct = default);
    Task<PreparationCalculateResult> PreviewAsync(PreparationInput input, CancellationToken ct = default);
}

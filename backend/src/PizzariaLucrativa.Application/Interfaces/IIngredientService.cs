using PizzariaLucrativa.Application.DTOs;

namespace PizzariaLucrativa.Application.Interfaces;

public interface IIngredientService
{
    Task<PagedResult<IngredientDto>> ListAsync(string? search, PagedRequest page, CancellationToken ct = default);
    Task<IngredientDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<IngredientDto> CreateAsync(IngredientInput input, CancellationToken ct = default);
    Task<IngredientDto> UpdateAsync(Guid id, IngredientInput input, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<IngredientUsagesDto> UsagesAsync(Guid id, CancellationToken ct = default);
    Task<IngredientDto> PreviewCostAsync(IngredientInput input, CancellationToken ct = default);
}

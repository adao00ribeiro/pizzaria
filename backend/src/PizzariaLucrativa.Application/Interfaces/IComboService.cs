using PizzariaLucrativa.Application.DTOs;

namespace PizzariaLucrativa.Application.Interfaces;

public interface IComboService
{
    Task<PagedResult<ComboDto>> ListAsync(PagedRequest page, CancellationToken ct = default);
    Task<ComboDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<ComboDto> CreateAsync(ComboInput input, CancellationToken ct = default);
    Task<ComboDto> UpdateAsync(Guid id, ComboInput input, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    /// <summary>Recalcula e persiste os derivados (POST /combos/{id}/calculate).</summary>
    Task<ComboDto> CalculateAsync(Guid id, CancellationToken ct = default);
}

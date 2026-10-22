using PizzariaLucrativa.Application.DTOs;

namespace PizzariaLucrativa.Application.Interfaces;

public interface IFixedCostService
{
    Task<IReadOnlyList<FixedCostDto>> ListAsync(CancellationToken ct = default);
    Task<FixedCostDto> CreateAsync(FixedCostInput input, CancellationToken ct = default);
    Task<FixedCostDto> UpdateAsync(Guid id, FixedCostInput input, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

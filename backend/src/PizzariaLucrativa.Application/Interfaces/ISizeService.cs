using PizzariaLucrativa.Application.DTOs;

namespace PizzariaLucrativa.Application.Interfaces;

public interface ISizeService
{
    Task<IReadOnlyList<SizeDto>> ListAsync(CancellationToken ct = default);
    Task<SizeDto> CreateAsync(SizeInput input, CancellationToken ct = default);
    Task<SizeDto> UpdateAsync(Guid id, SizeInput input, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

using PizzariaLucrativa.Application.DTOs;

namespace PizzariaLucrativa.Application.Interfaces;

public interface IFeeService
{
    Task<FeeConfigDto> GetAsync(CancellationToken ct = default);
    Task<FeeConfigDto> UpdateAsync(FeeConfigDto input, CancellationToken ct = default);
}

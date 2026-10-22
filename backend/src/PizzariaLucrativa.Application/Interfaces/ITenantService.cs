using PizzariaLucrativa.Application.DTOs;

namespace PizzariaLucrativa.Application.Interfaces;

public interface ITenantService
{
    Task<TenantDto> GetAsync(CancellationToken ct = default);
    Task<TenantDto> UpdateAsync(TenantUpdateInput input, CancellationToken ct = default);
}

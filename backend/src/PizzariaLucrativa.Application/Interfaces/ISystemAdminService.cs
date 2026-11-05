using PizzariaLucrativa.Application.DTOs;

namespace PizzariaLucrativa.Application.Interfaces;

/// <summary>
/// Contrato de serviço para governança global de tenants e ciclo de vida do administrador do sistema.
/// </summary>
public interface ISystemAdminService
{
    Task<PagedResult<TenantListItemDto>> GetTenantsAsync(int page, int pageSize, string? search, bool? isActive, CancellationToken ct = default);
    Task<TenantListItemDto> UpdateTenantStatusAsync(Guid tenantId, TenantStatusUpdateInput input, CancellationToken ct = default);
    Task<SystemAdminAuthResultDto> LoginAsync(SystemAdminLoginInput input, CancellationToken ct = default);
    Task ForgotPasswordAsync(ForgotPasswordInput input, CancellationToken ct = default);
    Task ResetPasswordAsync(ResetPasswordInput input, CancellationToken ct = default);
    Task ResetPasswordCliAsync(string email, string newPassword, CancellationToken ct = default);
}

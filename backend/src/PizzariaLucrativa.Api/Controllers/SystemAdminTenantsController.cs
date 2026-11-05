using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PizzariaLucrativa.Api.Controllers.Shared;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Interfaces;

namespace PizzariaLucrativa.Api.Controllers;

/// <summary>
/// Gestão e moderação global de tenants (pizzarias) pelo Administrador do Sistema.
/// </summary>
[Authorize(Policy = "SystemAdminOnly")]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/system-admin/tenants")]
public sealed class SystemAdminTenantsController : ApiControllerBase
{
    private readonly ISystemAdminService _adminService;

    public SystemAdminTenantsController(ISystemAdminService adminService)
    {
        _adminService = adminService;
    }

    [HttpGet]
    public Task<PagedResult<TenantListItemDto>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 15,
        [FromQuery] string? search = null,
        [FromQuery] bool? isActive = null,
        CancellationToken ct = default)
        => _adminService.GetTenantsAsync(page, pageSize, search, isActive, ct);

    [HttpPatch("{id:guid}/status")]
    public Task<TenantListItemDto> UpdateStatus(
        Guid id,
        [FromBody] TenantStatusUpdateInput input,
        CancellationToken ct = default)
        => _adminService.UpdateTenantStatusAsync(id, input, ct);
}

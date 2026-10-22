using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PizzariaLucrativa.Api.Controllers.Shared;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Interfaces;

namespace PizzariaLucrativa.Api.Controllers;

/// <summary>Dados da pizzaria (tenant) logada (docs/07 §13).</summary>
[Authorize]
[ApiVersion("1.0")]
public sealed class TenantController : ApiControllerBase
{
    private readonly ITenantService _tenant;

    public TenantController(ITenantService tenant) => _tenant = tenant;

    [HttpGet]
    public Task<TenantDto> Get(CancellationToken ct = default)
        => _tenant.GetAsync(ct);

    [HttpPut]
    public Task<TenantDto> Update(TenantUpdateInput input, CancellationToken ct = default)
        => _tenant.UpdateAsync(input, ct);
}

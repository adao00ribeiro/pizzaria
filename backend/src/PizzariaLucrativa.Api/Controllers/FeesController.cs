using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PizzariaLucrativa.Api.Controllers.Shared;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Interfaces;

namespace PizzariaLucrativa.Api.Controllers;

/// <summary>Configuração única de taxas e margem padrão do tenant (docs/07 §4).</summary>
[Authorize]
[ApiVersion("1.0")]
public sealed class FeesController : ApiControllerBase
{
    private readonly IFeeService _fees;

    public FeesController(IFeeService fees) => _fees = fees;

    [HttpGet]
    public Task<FeeConfigDto> Get(CancellationToken ct = default)
        => _fees.GetAsync(ct);

    [HttpPut]
    public Task<FeeConfigDto> Update(FeeConfigDto input, CancellationToken ct = default)
        => _fees.UpdateAsync(input, ct);
}

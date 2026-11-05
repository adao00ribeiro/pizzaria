using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PizzariaLucrativa.Api.Controllers.Shared;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Interfaces;

namespace PizzariaLucrativa.Api.Controllers;

/// <summary>
/// Autenticação e recuperação de credenciais do Administrador Global do Sistema (Plataforma).
/// </summary>
[AllowAnonymous]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/system-admin/auth")]
public sealed class SystemAdminAuthController : ApiControllerBase
{
    private readonly ISystemAdminService _adminService;

    public SystemAdminAuthController(ISystemAdminService adminService)
    {
        _adminService = adminService;
    }

    [HttpPost("login")]
    public Task<SystemAdminAuthResultDto> Login([FromBody] SystemAdminLoginInput input, CancellationToken ct = default)
        => _adminService.LoginAsync(input, ct);

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordInput input, CancellationToken ct = default)
    {
        await _adminService.ForgotPasswordAsync(input, ct);
        return Ok(new { message = "Se o e-mail informado pertencer ao administrador, as instruções foram processadas." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordInput input, CancellationToken ct = default)
    {
        await _adminService.ResetPasswordAsync(input, ct);
        return Ok(new { message = "Senha redefinida com sucesso." });
    }
}

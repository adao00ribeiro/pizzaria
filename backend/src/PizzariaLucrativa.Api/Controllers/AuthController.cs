using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PizzariaLucrativa.Api.Controllers.Shared;
using PizzariaLucrativa.Application.Interfaces;

namespace PizzariaLucrativa.Api.Controllers;

/// <summary>Registro, login e renovação de sessão (docs/07 §1 — rotas públicas).</summary>
[AllowAnonymous]
[ApiVersion("1.0")]
public sealed class AuthController : ApiControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    /// <summary>Cria pizzaria (tenant) + usuário dono e devolve os tokens.</summary>
    [HttpPost("register")]
    [EnableRateLimiting("login")]
    public Task<AuthResult> Register(RegisterCommand command, CancellationToken ct = default)
        => _auth.RegisterAsync(command, ct);

    /// <summary>Login com e-mail/senha (rate limit 5/min por IP — NF-T02).</summary>
    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public Task<AuthResult> Login(LoginCommand command, CancellationToken ct = default)
        => _auth.LoginAsync(command, ct);

    /// <summary>Troca um refresh token válido por um novo par (rotação — NF-T01).</summary>
    [HttpPost("refresh")]
    public Task<AuthResult> Refresh(RefreshCommand command, CancellationToken ct = default)
        => _auth.RefreshAsync(command, ct);
}

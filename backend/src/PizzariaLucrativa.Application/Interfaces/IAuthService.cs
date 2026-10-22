namespace PizzariaLucrativa.Application.Interfaces;

/// <summary>Autenticação (register/login/refresh) — implementado na Infrastructure (Identity + JWT).</summary>
public interface IAuthService
{
    Task<AuthResult> RegisterAsync(RegisterCommand command, CancellationToken ct = default);

    Task<AuthResult> LoginAsync(LoginCommand command, CancellationToken ct = default);

    Task<AuthResult> RefreshAsync(RefreshCommand command, CancellationToken ct = default);
}

public sealed record RegisterCommand(string Email, string Password, string FullName, string PizzeriaName);

public sealed record LoginCommand(string Email, string Password);

public sealed record RefreshCommand(string RefreshToken);

public sealed record AuthResult(string AccessToken, string RefreshToken, int ExpiresIn);

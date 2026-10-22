namespace PizzariaLucrativa.Application.Interfaces;

/// <summary>
/// Gestão de usuários e papéis do tenant (docs/10, Sprint 1).
/// Implementado na Infrastructure (ASP.NET Identity).
/// </summary>
public interface IUserAdminService
{
    Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken ct = default);

    Task<UserDto> CreateAsync(CreateUserInput input, CancellationToken ct = default);

    Task<UserDto> UpdateAsync(Guid id, UpdateUserInput input, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>Papéis disponíveis (owner / atendente / pizzaiolo).</summary>
    Task<IReadOnlyList<string>> RolesAsync(CancellationToken ct = default);
}

public sealed record UserDto(
    Guid Id,
    string Email,
    string FullName,
    string Role,
    bool IsOwner,
    DateTime CreatedAt);

public sealed record CreateUserInput(string Email, string Password, string FullName, string Role);

public sealed record UpdateUserInput(string? FullName, string? Role);

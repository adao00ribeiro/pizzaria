namespace PizzariaLucrativa.Infrastructure.Persistence;

/// <summary>
/// Papéis funcionais do produto (docs/10, premissa P4). São os nomes gravados no
/// Identity (AspNetRoles) e no claim <c>role</c> do JWT.
/// </summary>
public static class AppRoles
{
    public const string Owner = "owner";
    public const string Atendente = "atendente";
    public const string Pizzaiolo = "pizzaiolo";
    public const string Garcom = "garcom";

    public static readonly IReadOnlyList<string> All = [Owner, Atendente, Pizzaiolo, Garcom];

    public static bool IsValid(string? role)
        => role is not null && All.Contains(role, StringComparer.OrdinalIgnoreCase);

    /// <summary>Normaliza para o nome canônico (minúsculo) ou devolve null se inválido.</summary>
    public static string? Normalize(string? role)
        => role is null ? null : All.FirstOrDefault(r => string.Equals(r, role.Trim(), StringComparison.OrdinalIgnoreCase));
}

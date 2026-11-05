namespace PizzariaLucrativa.Domain.Entities;

/// <summary>
/// Administrador Global do Sistema (Dono da Plataforma).
/// Segregado da tabela de usuários de restaurantes e de TenantEntity.
/// Invariante de cardinalidade unitária: estritamente 1 conta singleton.
/// </summary>
public class SystemAdmin
{
    public static readonly Guid SingletonId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public Guid Id { get; set; } = SingletonId;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;

    // Recuperação de senha temporária
    public string? PasswordResetTokenHash { get; set; }
    public DateTime? PasswordResetTokenExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

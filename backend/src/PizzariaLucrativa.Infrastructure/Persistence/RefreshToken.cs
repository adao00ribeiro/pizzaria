namespace PizzariaLucrativa.Infrastructure.Persistence;

/// <summary>
/// Refresh token persistido (hash SHA-256, nunca o valor em claro). Validade de 7 dias
/// com rotação a cada uso: ao trocar por um novo, o anterior é revogado (NF-T01).
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string UserId { get; set; } = string.Empty;

    public AppUser? User { get; set; }

    /// <summary>SHA-256 hex do token bruto.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresAt { get; set; }

    /// <summary>Preenchido quando rotacionado ou revogado.</summary>
    public DateTime? RevokedAt { get; set; }

    public bool IsActive => RevokedAt is null && ExpiresAt > DateTime.UtcNow;
}

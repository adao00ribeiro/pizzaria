using Microsoft.AspNetCore.Identity;

namespace PizzariaLucrativa.Infrastructure.Persistence;

/// <summary>
/// Usuário Identity vinculado a um tenant (pizzaria). O TenantId vem sempre do cadastro
/// e é lido do claim do token — nunca do corpo da requisição (NF-S04).
/// </summary>
public class AppUser : IdentityUser
{
    public Guid TenantId { get; set; }

    public string FullName { get; set; } = string.Empty;

    /// <summary>Data de criação (exibido no cadastro de equipe — docs/10, Sprint 1).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

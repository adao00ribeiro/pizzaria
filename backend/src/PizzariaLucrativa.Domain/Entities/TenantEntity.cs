namespace PizzariaLucrativa.Domain.Entities;

/// <summary>
/// Base de todas as entidades de negócio: todo registro pertence a um tenant (pizzaria)
/// e o TenantId é sempre resolvido do token — nunca do corpo da requisição (NF-S04).
/// </summary>
public abstract class TenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

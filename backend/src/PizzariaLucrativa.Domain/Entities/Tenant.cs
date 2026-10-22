namespace PizzariaLucrativa.Domain.Entities;

/// <summary>Pizzaria (tenant). Não herda de TenantEntity por ser a própria raiz do isolamento.</summary>
public class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Identificador público usado no cardápio/site (docs/10, Sprint 1).
    /// Único por tenant; nulo apenas em tenants legados até o backfill do startup.
    /// </summary>
    public string? Slug { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

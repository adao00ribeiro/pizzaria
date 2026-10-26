using PizzariaLucrativa.Domain.Enums;

namespace PizzariaLucrativa.Domain.Entities;

/// <summary>
/// Catálogo de bairros da pizzaria (docs/sprints, IA-1/IA-2). Cada bairro pertence a uma
/// <see cref="NeighborhoodRegion"/> e é usado para classificar pedidos de delivery.
/// A unicidade de <see cref="Name"/> por tenant é garantida no serviço porque o TPT
/// impede o índice único composto com <see cref="TenantEntity.TenantId"/>.
/// </summary>
public class Neighborhood : TenantEntity
{
    /// <summary>Nome do bairro (obrigatório, normalizado no serviço).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Região macro usada nos relatórios e gráficos.</summary>
    public NeighborhoodRegion Region { get; set; } = NeighborhoodRegion.Other;

    /// <summary>Permite desativar o bairro sem excluir o histórico de pedidos.</summary>
    public bool IsActive { get; set; } = true;
}

namespace PizzariaLucrativa.Domain.Enums;

/// <summary>
/// Região macro da cidade usada para agrupar entregas e alimentar os relatórios/gráficos
/// do assistente de IA (docs/sprints, IA-1). O valor é congelado no pedido em
/// <see cref="Entities.Order.RegionSnapshot"/> para preservar o histórico.
/// </summary>
public enum NeighborhoodRegion
{
    /// <summary>Centro / região central.</summary>
    Center = 0,

    /// <summary>Zona Norte.</summary>
    North = 1,

    /// <summary>Zona Sul.</summary>
    South = 2,

    /// <summary>Zona Leste.</summary>
    East = 3,

    /// <summary>Zona Oeste.</summary>
    West = 4,

    /// <summary>Região não mapeada / outra localidade.</summary>
    Other = 5
}

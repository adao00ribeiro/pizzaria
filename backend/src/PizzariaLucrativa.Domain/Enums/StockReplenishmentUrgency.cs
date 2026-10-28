namespace PizzariaLucrativa.Domain.Enums;

/// <summary>
/// Nível de urgência na reposição de estoque do insumo para compras.
/// </summary>
public enum StockReplenishmentUrgency
{
    /// <summary>Estoque zerado ou negativo — impacto imediato na produção e vendas.</summary>
    Critical = 0,

    /// <summary>Estoque abaixo ou igual ao mínimo definido.</summary>
    Low = 1,

    /// <summary>Estoque próximo ao mínimo (até 25% acima do mínimo).</summary>
    Warning = 2,

    /// <summary>Estoque confortável acima do mínimo.</summary>
    Normal = 3
}

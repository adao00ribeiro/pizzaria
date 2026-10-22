namespace PizzariaLucrativa.Domain.Enums;

public enum AlertType
{
    /// <summary>Item ficou com margem abaixo do alvo após recálculo.</summary>
    MarginBelow = 0,

    /// <summary>Estoque do insumo abaixo do mínimo.</summary>
    LowStock = 1
}

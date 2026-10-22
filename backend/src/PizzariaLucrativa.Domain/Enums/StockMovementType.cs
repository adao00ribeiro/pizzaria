namespace PizzariaLucrativa.Domain.Enums;

public enum StockMovementType
{
    In = 0,
    Out = 1,
    Adjustment = 2,

    /// <summary>Baixa gerada automaticamente por um pedido (OrderId preenchido).</summary>
    OrderOut = 3
}

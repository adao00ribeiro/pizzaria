namespace PizzariaLucrativa.Domain.Enums;

/// <summary>Origem/canal do pedido (docs/10, Sprint 2).</summary>
public enum OrderType
{
    /// <summary>Consumido no balcão — sem entrega.</summary>
    Counter = 0,

    /// <summary>Retirada na pizzaria — sem taxa de entrega.</summary>
    Pickup = 1,

    /// <summary>Entrega no endereço do cliente — sujeito à taxa de entrega.</summary>
    Delivery = 2
}

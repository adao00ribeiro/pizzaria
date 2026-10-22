namespace PizzariaLucrativa.Domain.Enums;

/// <summary>
/// Ciclo de vida do pedido (docs/10, Sprint 2). As transições legais são
/// validadas em <c>OrderService.ChangeStatusAsync</c>.
/// </summary>
public enum OrderStatus
{
    /// <summary>Pedido recebido, aguardando a produção (coluna "Recebido").</summary>
    Received = 0,

    /// <summary>Em preparo na cozinha (coluna "Em preparo").</summary>
    InPreparation = 1,

    /// <summary>Pronto — a baixa de estoque acontece aqui (premissa P3).</summary>
    Ready = 2,

    /// <summary>Saiu para entrega (apenas pedidos de entrega).</summary>
    OutForDelivery = 3,

    /// <summary>Entregue/concluído — entra no faturamento (premissa P6).</summary>
    Completed = 4,

    /// <summary>Cancelado — motivo obrigatório; estoque estornado se já baixado.</summary>
    Cancelled = 5
}

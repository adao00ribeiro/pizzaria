using PizzariaLucrativa.Domain.Enums;

namespace PizzariaLucrativa.Domain.Entities;

/// <summary>
/// Pedido (docs/10, Sprint 2). Nasce no balcão (tablet do atendente — Sprint 4)
/// ou no site público sem login (Sprint 5). O <see cref="Code"/> é o código curto
/// de acompanhamento por tenant.
/// </summary>
public class Order : TenantEntity
{
    /// <summary>Código alfanumérico de 6 caracteres (alfabeto sem O/0/I/1), único por tenant.</summary>
    public string Code { get; set; } = string.Empty;

    public OrderStatus Status { get; set; } = OrderStatus.Received;

    public OrderType Type { get; set; } = OrderType.Counter;

    // ---- Dados do cliente (exigidos no checkout público e em delivery) ----
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }

    /// <summary>Bairro em texto livre — mantido para compatibilidade e pedidos históricos.</summary>
    public string? AddressDistrict { get; set; }
    public string? AddressStreet { get; set; }
    public string? AddressNumber { get; set; }
    public string? AddressComplement { get; set; }

    // ---- Granularidade de entrega (docs/sprints, IA-1) ----
    /// <summary>Bairro do catálogo do tenant; nulo para balcão/retirada ou bairro não cadastrado.</summary>
    public Guid? NeighborhoodId { get; set; }
    public Neighborhood? Neighborhood { get; set; }

    /// <summary>
    /// Região congelada no momento do pedido. Nulo = "Não classificado" nos relatórios;
    /// pedidos antigos continuam válidos sem remapeamento.
    /// </summary>
    public NeighborhoodRegion? RegionSnapshot { get; set; }

    // ---- Valores (snapshots no momento do pedido) ----
    public decimal Subtotal { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal Discount { get; set; }
    public decimal Total { get; set; }

    public string? Notes { get; set; }

    /// <summary>Usuário que lançou o pedido; null = pedido anônimo do site público.</summary>
    public string? PlacedByUserId { get; set; }

    /// <summary>Forma de pagamento informada (P8) — usada só no ticket e na estimativa de taxa.</summary>
    public PaymentMethod? PaymentMethod { get; set; }

    // ---- Linha do tempo ----
    public DateTime PlacedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? ReadyAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }

    public List<OrderItem> Items { get; set; } = new();
}

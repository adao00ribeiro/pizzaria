namespace PizzariaLucrativa.Domain.Entities;

/// <summary>
/// Item de pedido (docs/10, Sprint 2): uma pizza (com tamanho e 1–4 sabores),
/// uma bebida (sabor único) ou um combo. Preço e custo são snapshots — o pedido
/// não é recalculado quando o catálogo muda.
/// </summary>
public class OrderItem : TenantEntity
{
    public Guid OrderId { get; set; }
    public Order? Order { get; set; }

    /// <summary>Tamanho da pizza; null para bebidas e combos.</summary>
    public Guid? SizeId { get; set; }
    public Size? Size { get; set; }

    /// <summary>Combo vendido como item; null para pizza/bebida avulsa.</summary>
    public Guid? ComboId { get; set; }
    public Combo? Combo { get; set; }

    public int Quantity { get; set; } = 1;

    /// <summary>Preço unitário snapshot (CardPrice ?? SuggestedPrice, ou Combo.TotalPrice).</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Custo unitário snapshot (custo real da ficha ou do combo).</summary>
    public decimal CostSnapshot { get; set; }

    public decimal Total { get; set; }

    public string? Notes { get; set; }

    public List<OrderItemFlavor> Flavors { get; set; } = new();
}

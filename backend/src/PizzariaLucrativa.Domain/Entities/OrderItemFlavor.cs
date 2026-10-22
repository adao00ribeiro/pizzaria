namespace PizzariaLucrativa.Domain.Entities;

/// <summary>
/// Sabor de um item de pedido (docs/10, Sprint 2). Até 4 por item; a soma das
/// frações é 100%. Item simples (bebida/sabor único) usa uma única fração de 100.
/// </summary>
public class OrderItemFlavor : TenantEntity
{
    public Guid OrderItemId { get; set; }
    public OrderItem? OrderItem { get; set; }

    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    /// <summary>Fração do sabor no item: 50/50, 25/25/25/25 ou 100 (sabor único).</summary>
    public decimal SharePercentage { get; set; } = 100m;
}

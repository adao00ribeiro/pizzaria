namespace PizzariaLucrativa.Domain.Entities;

/// <summary>Item de combo: produto (pizza com tamanho, ou bebida sem tamanho).</summary>
public class ComboItem : TenantEntity
{
    public Guid ComboId { get; set; }
    public Combo? Combo { get; set; }

    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    /// <summary>null para bebidas.</summary>
    public Guid? SizeId { get; set; }
    public Size? Size { get; set; }

    public int Quantity { get; set; } = 1;
}

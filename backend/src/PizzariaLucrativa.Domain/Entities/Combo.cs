using PizzariaLucrativa.Domain.Enums;

namespace PizzariaLucrativa.Domain.Entities;

/// <summary>Combo: pizzas (sabor+tamanho) e/ou bebidas com desconto e preço calculado.</summary>
public class Combo : TenantEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Desconto em R$ aplicado sobre custo e preço.</summary>
    public decimal DiscountValue { get; set; }

    /// <summary>Preço fechado opcional (sobrepõe a soma dos preços).</summary>
    public decimal? FixedPrice { get; set; }

    // ---- Derivados (persistidos) ----
    public decimal TotalCost { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal TaxValue { get; set; }
    public decimal Profit { get; set; }
    public decimal MarginPercentage { get; set; }
    public MarginStatus MarginStatus { get; set; }

    public List<ComboItem> Items { get; set; } = new();
}

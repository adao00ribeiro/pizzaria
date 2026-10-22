using PizzariaLucrativa.Domain.Enums;

namespace PizzariaLucrativa.Domain.Entities;

/// <summary>
/// Ficha técnica = produto × tamanho. Os campos derivados (custo, preço, margem, status)
/// são persistidos para leitura rápida no dashboard; a verdade fonte são os insumos —
/// toda alteração de custo dispara o recálculo em cadeia.
/// </summary>
public class TechnicalSheet : TenantEntity
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public Guid SizeId { get; set; }
    public Size Size { get; set; } = null!;

    /// <summary>Custo de embalagem deste tamanho.</summary>
    public decimal PackagingCost { get; set; }

    /// <summary>Perda adicional da ficha (% sobre o subtotal). Opcional.</summary>
    public decimal? LossPercentage { get; set; }

    /// <summary>Sobrescreve a margem padrão do tenant. Opcional.</summary>
    public decimal? MarginPercentage { get; set; }

    // ---- Derivados (persistidos) ----
    public decimal DirectCost { get; set; }
    public decimal IndirectCost { get; set; }
    public decimal TotalCost { get; set; }
    public decimal SuggestedPrice { get; set; }

    /// <summary>Preço de cardápio sugerido (arredondado para terminar em ,90).</summary>
    public decimal? CardPrice { get; set; }

    public decimal TaxValue { get; set; }
    public decimal EstimatedProfit { get; set; }
    public decimal MarginPercentageReal { get; set; }
    public MarginStatus MarginStatus { get; set; }

    public List<TechnicalSheetItem> Items { get; set; } = new();
}

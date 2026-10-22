using PizzariaLucrativa.Domain.Enums;

namespace PizzariaLucrativa.Domain.Entities;

/// <summary>Insumo/ingrediente comprado a granel (ex.: saco de 5000 g de muçarela).</summary>
public class Ingredient : TenantEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Unidade em que o fornecedor vende (g, ml, un, kg, L).</summary>
    public MeasureUnit PurchaseUnit { get; set; }

    /// <summary>Quantidade na embalagem comprada (ex.: 5000).</summary>
    public decimal PurchaseQuantity { get; set; }

    /// <summary>Preço pago na compra (ex.: 32.90).</summary>
    public decimal PurchasePrice { get; set; }

    /// <summary>% de perda/desperdício aplicada sobre o custo de compra.</summary>
    public decimal LossPercentage { get; set; }

    /// <summary>Custo derivado por unidade de compra (com perda).</summary>
    public decimal UnitCost { get; set; }

    /// <summary>Estoque mínimo para gerar alerta de reposição.</summary>
    public decimal MinimumStock { get; set; }

    /// <summary>Saldo derivado dos movimentos de estoque.</summary>
    public decimal CurrentStock { get; set; }

    /// <summary>Multiplicador aplicado no preço de venda quando o insumo é usado como bebida avulsa.</summary>
    public decimal MarkupMultiplier { get; set; } = 2.0m;

    /// <summary>Versão de custo — incrementa a cada mudança de preço (invalida cache, NF-P04).</summary>
    public int CostVersion { get; set; }
}

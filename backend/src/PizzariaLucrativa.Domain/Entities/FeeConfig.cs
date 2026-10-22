namespace PizzariaLucrativa.Domain.Entities;

/// <summary>Configuração única de precificação do tenant (taxas e margem padrão).</summary>
public class FeeConfig
{
    /// <summary>PK = TenantId (uma configuração por pizzaria).</summary>
    public Guid TenantId { get; set; }

    /// <summary>% de taxa de cartão/depositaria (ex.: 2.99).</summary>
    public decimal CardFeePercentage { get; set; }

    /// <summary>% de taxa de delivery/apps (ex.: 25) — incide sobre o preço cobrado.</summary>
    public decimal DeliveryFeePercentage { get; set; }

    /// <summary>Valor fixo de entrega (opcional, informado pelo usuário no cálculo de referência).</summary>
    public decimal DeliveryFixedValue { get; set; }

    /// <summary>Margem desejada padrão (% sobre o preço de venda).</summary>
    public decimal DefaultMarginPercentage { get; set; } = 60;

    /// <summary>% de perda global opcional aplicada a todos os insumos.</summary>
    public decimal LossPercentageGlobal { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

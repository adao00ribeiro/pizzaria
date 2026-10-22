namespace PizzariaLucrativa.Domain.Entities;

/// <summary>Tamanho de pizza (Broto 20 cm, Média 30 cm...).</summary>
public class Size : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public int? DiameterCm { get; set; }
}

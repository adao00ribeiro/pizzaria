using PizzariaLucrativa.Domain.Enums;

namespace PizzariaLucrativa.Domain.Entities;

/// <summary>Receita base (ex.: "Massa broto", "Molho de tomate") composta por ingredientes e/ou outros preparos.</summary>
public class Preparation : TenantEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Quantidade produzida por "receita" (ex.: 10 unidades de massa).</summary>
    public decimal YieldQuantity { get; set; }

    public MeasureUnit YieldUnit { get; set; } = MeasureUnit.un;

    /// <summary>Custo total derivado da soma dos itens.</summary>
    public decimal TotalCost { get; set; }

    /// <summary>Custo derivado por unidade de rendimento (TotalCost ÷ YieldQuantity).</summary>
    public decimal UnitCost { get; set; }

    public List<PreparationItem> Items { get; set; } = new();
}

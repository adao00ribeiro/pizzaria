using PizzariaLucrativa.Domain.Enums;

namespace PizzariaLucrativa.Domain.Entities;

/// <summary>Item de ficha técnica: ingrediente OU preparo, com quantidade e custo calculado.</summary>
public class TechnicalSheetItem : TenantEntity
{
    public Guid TechnicalSheetId { get; set; }
    public TechnicalSheet? TechnicalSheet { get; set; }

    public SheetItemType Type { get; set; }

    public Guid? IngredientId { get; set; }
    public Ingredient? Ingredient { get; set; }

    public Guid? PreparationId { get; set; }
    public Preparation? Preparation { get; set; }

    public decimal Quantity { get; set; }

    public MeasureUnit Unit { get; set; } = MeasureUnit.g;

    /// <summary>Custo unitário do insumo no momento do cálculo (auditoria).</summary>
    public decimal UnitCostSnapshot { get; set; }

    /// <summary>Custo derivado deste item (quantidade × custo unitário).</summary>
    public decimal ItemCost { get; set; }
}

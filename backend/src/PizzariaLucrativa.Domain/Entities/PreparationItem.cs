using PizzariaLucrativa.Domain.Enums;

namespace PizzariaLucrativa.Domain.Entities;

/// <summary>
/// Item de preparo: aponta obrigatoriamente para um ingrediente OU para outro preparo
/// (exclusivo — validado no serviço de aplicação e por CHECK no banco quando suportado).
/// </summary>
public class PreparationItem : TenantEntity
{
    public Guid PreparationId { get; set; }
    public Preparation? Preparation { get; set; }

    public Guid? IngredientId { get; set; }
    public Ingredient? Ingredient { get; set; }

    /// <summary>Preparo aninhado (ex.: massa dentro de uma recheia). ≠ PreparationId (ciclo resolvido no cálculo).</summary>
    public Guid? SourcePreparationId { get; set; }
    public Preparation? SourcePreparation { get; set; }

    /// <summary>
    /// Origem do item derivada das FKs exclusivas (IngredientId XOR SourcePreparationId).
    /// Propriedade calculada — não é coluna.
    /// </summary>
    public SheetItemType Type => IngredientId.HasValue ? SheetItemType.Ingredient : SheetItemType.Preparation;

    public decimal Quantity { get; set; }

    public MeasureUnit Unit { get; set; } = MeasureUnit.g;
}

using PizzariaLucrativa.Domain.Enums;

namespace PizzariaLucrativa.Domain.Entities;

/// <summary>Produto do cardápio (sabor de pizza, bebida ou outro).</summary>
public class Product : TenantEntity
{
    public string Name { get; set; } = string.Empty;

    public ProductCategory Category { get; set; } = ProductCategory.Pizza;

    /// <summary>Permite ocultar do cardápio sem excluir.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>URL da imagem do produto para exibição no cardápio e na produção.</summary>
    public string? ImageUrl { get; set; }

    /// <summary>Preço de aquisição — usado apenas para bebidas (docs/06 §10).</summary>
    public decimal? PurchasePrice { get; set; }

    /// <summary>Multiplicador de markup da bebida (docs/06 §10). Padrão 2.0 (dobro do custo).</summary>
    public decimal? MarkupMultiplier { get; set; }

    /// <summary>
    /// Insumo vinculado à bebida (docs/10, Sprint 2): quando informado, vender a
    /// bebida também debita esse ingrediente do estoque. Opcional.
    /// </summary>
    public Guid? IngredientId { get; set; }
    public Ingredient? Ingredient { get; set; }
}

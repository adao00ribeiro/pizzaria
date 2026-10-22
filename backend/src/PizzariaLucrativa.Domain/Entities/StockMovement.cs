using PizzariaLucrativa.Domain.Enums;

namespace PizzariaLucrativa.Domain.Entities;

/// <summary>Movimento de estoque de um insumo. O saldo (CurrentStock) é derivado destes registros.</summary>
public class StockMovement : TenantEntity
{
    public Guid IngredientId { get; set; }
    public Ingredient? Ingredient { get; set; }

    public StockMovementType Type { get; set; }

    public decimal Quantity { get; set; }

    /// <summary>Custo unitário da entrada (histórico de preço de compra, RF-04).</summary>
    public decimal? UnitCost { get; set; }

    /// <summary>Pedido que originou a baixa (OrderOut) ou o estorno; null em movimentos manuais.</summary>
    public Guid? OrderId { get; set; }
    public Order? Order { get; set; }

    public string? Note { get; set; }
}

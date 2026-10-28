using PizzariaLucrativa.Domain.Enums;
using PizzariaLucrativa.Domain.Services;
using Xunit;

namespace PizzariaLucrativa.Domain.Tests;

public class StockReplenishmentCalculatorTests
{
    [Fact]
    public void ResolveUrgency_ShouldClassifyCritical_WhenStockIsZeroOrNegative()
    {
        Assert.Equal(StockReplenishmentUrgency.Critical, StockReplenishmentCalculator.ResolveUrgency(-5m, 10m));
        Assert.Equal(StockReplenishmentUrgency.Critical, StockReplenishmentCalculator.ResolveUrgency(0m, 10m));
        Assert.Equal(StockReplenishmentUrgency.Critical, StockReplenishmentCalculator.ResolveUrgency(0m, 0m));
    }

    [Fact]
    public void ResolveUrgency_ShouldClassifyLow_WhenStockIsPositiveAndBelowOrEqualToMinimum()
    {
        Assert.Equal(StockReplenishmentUrgency.Low, StockReplenishmentCalculator.ResolveUrgency(5m, 10m));
        Assert.Equal(StockReplenishmentUrgency.Low, StockReplenishmentCalculator.ResolveUrgency(10m, 10m));
    }

    [Fact]
    public void ResolveUrgency_ShouldClassifyWarning_WhenStockIsWithin25PercentAboveMinimum()
    {
        // 10 * 1.25 = 12.5
        Assert.Equal(StockReplenishmentUrgency.Warning, StockReplenishmentCalculator.ResolveUrgency(11m, 10m));
        Assert.Equal(StockReplenishmentUrgency.Warning, StockReplenishmentCalculator.ResolveUrgency(12.5m, 10m));
    }

    [Fact]
    public void ResolveUrgency_ShouldClassifyNormal_WhenStockIsComfortablyAboveMinimum()
    {
        Assert.Equal(StockReplenishmentUrgency.Normal, StockReplenishmentCalculator.ResolveUrgency(12.51m, 10m));
        Assert.Equal(StockReplenishmentUrgency.Normal, StockReplenishmentCalculator.ResolveUrgency(20m, 10m));
        Assert.Equal(StockReplenishmentUrgency.Normal, StockReplenishmentCalculator.ResolveUrgency(1m, 0m));
    }

    [Fact]
    public void CalculateItem_ShouldCalculateCorrectDeficitAndCeilingPackages()
    {
        // Insumo: Muçarela, pacote de 1000g a R$ 45.00. Mínimo 3000g, Atual 1200g.
        // Déficit = 1800g. Pacotes necessários = ceil(1800 / 1000) = 2 pacotes.
        // Reposição = 2000g. Custo = 2 * 45 = 90.00. Urgência = Low.
        var id = Guid.NewGuid();
        var result = StockReplenishmentCalculator.CalculateItem(
            ingredientId: id,
            name: "Muçarela Especial",
            unit: MeasureUnit.g,
            purchaseQuantity: 1000m,
            purchasePrice: 45m,
            unitCost: 0.045m,
            minimumStock: 3000m,
            currentStock: 1200m);

        Assert.Equal(id, result.IngredientId);
        Assert.Equal(1800m, result.DeficitQuantity);
        Assert.Equal(2, result.SuggestedPackagesToBuy);
        Assert.Equal(2000m, result.SuggestedReplenishmentQuantity);
        Assert.Equal(90m, result.EstimatedCost);
        Assert.Equal(StockReplenishmentUrgency.Low, result.Urgency);
    }

    [Fact]
    public void CalculateItem_WhenCurrentStockIsNegativeAndMinimumZero_ShouldCoverNegative()
    {
        // Saldo = -500g, Mínimo = 0g. Pacote de 1000g a R$ 30.
        // Déficit = Math.Max(0, 0 - (-500)) = 500g.
        // Pacotes = ceil(500 / 1000) = 1 pacote.
        var result = StockReplenishmentCalculator.CalculateItem(
            ingredientId: Guid.NewGuid(),
            name: "Farinha 00",
            unit: MeasureUnit.g,
            purchaseQuantity: 1000m,
            purchasePrice: 30m,
            unitCost: 0.03m,
            minimumStock: 0m,
            currentStock: -500m);

        Assert.Equal(500m, result.DeficitQuantity);
        Assert.Equal(1, result.SuggestedPackagesToBuy);
        Assert.Equal(1000m, result.SuggestedReplenishmentQuantity);
        Assert.Equal(30m, result.EstimatedCost);
        Assert.Equal(StockReplenishmentUrgency.Critical, result.Urgency);
    }

    [Fact]
    public void CalculateItem_WhenStockIsComfortable_ShouldHaveZeroDeficitAndZeroCost()
    {
        var result = StockReplenishmentCalculator.CalculateItem(
            ingredientId: Guid.NewGuid(),
            name: "Azeite de Oliva",
            unit: MeasureUnit.ml,
            purchaseQuantity: 500m,
            purchasePrice: 38.50m,
            unitCost: 0.077m,
            minimumStock: 1000m,
            currentStock: 1500m);

        Assert.Equal(0m, result.DeficitQuantity);
        Assert.Equal(0, result.SuggestedPackagesToBuy);
        Assert.Equal(0m, result.SuggestedReplenishmentQuantity);
        Assert.Equal(0m, result.EstimatedCost);
        Assert.Equal(StockReplenishmentUrgency.Normal, result.Urgency);
    }

    [Fact]
    public void CalculateItem_ShouldThrowDomainException_WhenInputIsNull()
    {
        Assert.Throws<PizzariaLucrativa.Domain.Exceptions.DomainException>(() =>
            StockReplenishmentCalculator.CalculateItem((StockReplenishmentItemInput)null!));
    }

    [Fact]
    public void CalculateItem_ShouldUseUnitCost_WhenPurchasePriceIsZero()
    {
        // PurchaseQuantity = 1000g, PurchasePrice = 0, UnitCost = 0.05. Mínimo 2000g, Atual 1000g.
        // Déficit = 1000g. Pacotes = 1. Reposição = 1000g. Custo = 1000 * 0.05 = 50.00.
        var result = StockReplenishmentCalculator.CalculateItem(
            ingredientId: Guid.NewGuid(),
            name: "Manjericão Fresco",
            unit: MeasureUnit.g,
            purchaseQuantity: 1000m,
            purchasePrice: 0m,
            unitCost: 0.05m,
            minimumStock: 2000m,
            currentStock: 1000m);

        Assert.Equal(1000m, result.DeficitQuantity);
        Assert.Equal(1, result.SuggestedPackagesToBuy);
        Assert.Equal(1000m, result.SuggestedReplenishmentQuantity);
        Assert.Equal(50m, result.EstimatedCost);
        Assert.Equal(StockReplenishmentUrgency.Low, result.Urgency);
    }

    [Fact]
    public void CalculateItem_ShouldHandleZeroPurchaseQuantity_UsingCeilingOnDeficitQuantity()
    {
        // PurchaseQuantity = 0, PurchasePrice = 0, UnitCost = 10. Mínimo 5, Atual 2.5.
        // Déficit = 2.5. Pacotes = ceil(2.5) = 3. Reposição = 2.5. Custo = 2.5 * 10 = 25.00.
        var result = StockReplenishmentCalculator.CalculateItem(
            ingredientId: Guid.NewGuid(),
            name: "Item Avulso",
            unit: MeasureUnit.un,
            purchaseQuantity: 0m,
            purchasePrice: 0m,
            unitCost: 10m,
            minimumStock: 5m,
            currentStock: 2.5m);

        Assert.Equal(2.5m, result.DeficitQuantity);
        Assert.Equal(3, result.SuggestedPackagesToBuy);
        Assert.Equal(2.5m, result.SuggestedReplenishmentQuantity);
        Assert.Equal(25m, result.EstimatedCost);
        Assert.Equal(StockReplenishmentUrgency.Low, result.Urgency);
    }

    [Fact]
    public void CalculateItem_ShouldSanitizeNegativeInputValues_Defensively()
    {
        // Valores negativos em PurchaseQuantity, PurchasePrice, UnitCost, MinimumStock devem ser protegidos com Math.Max(0, ...)
        var result = StockReplenishmentCalculator.CalculateItem(
            ingredientId: Guid.NewGuid(),
            name: "Item Negativo",
            unit: MeasureUnit.g,
            purchaseQuantity: -100m,
            purchasePrice: -50m,
            unitCost: -5m,
            minimumStock: -10m,
            currentStock: 10m);

        Assert.Equal(0m, result.PurchaseQuantity);
        Assert.Equal(0m, result.PurchasePrice);
        Assert.Equal(0m, result.UnitCost);
        Assert.Equal(0m, result.MinimumStock);
        Assert.Equal(0m, result.DeficitQuantity);
        Assert.Equal(0, result.SuggestedPackagesToBuy);
        Assert.Equal(0m, result.EstimatedCost);
        Assert.Equal(StockReplenishmentUrgency.Normal, result.Urgency);
    }

    [Fact]
    public void CalculateItem_WhenStockIsAtWarningThreshold_ShouldHaveWarningUrgencyAndZeroDeficit()
    {
        // Min = 1000g, Atual = 1200g (1200 <= 1000 * 1.25 = 1250)
        var result = StockReplenishmentCalculator.CalculateItem(
            ingredientId: Guid.NewGuid(),
            name: "Provolone",
            unit: MeasureUnit.g,
            purchaseQuantity: 1000m,
            purchasePrice: 60m,
            unitCost: 0.06m,
            minimumStock: 1000m,
            currentStock: 1200m);

        Assert.Equal(0m, result.DeficitQuantity);
        Assert.Equal(0, result.SuggestedPackagesToBuy);
        Assert.Equal(0m, result.SuggestedReplenishmentQuantity);
        Assert.Equal(0m, result.EstimatedCost);
        Assert.Equal(StockReplenishmentUrgency.Warning, result.Urgency);
    }

    [Theory]
    [InlineData(-10.0, 100.0, StockReplenishmentUrgency.Critical)]
    [InlineData(0.0, 100.0, StockReplenishmentUrgency.Critical)]
    [InlineData(0.0, 0.0, StockReplenishmentUrgency.Critical)]
    [InlineData(0.001, 100.0, StockReplenishmentUrgency.Low)]
    [InlineData(99.999, 100.0, StockReplenishmentUrgency.Low)]
    [InlineData(100.0, 100.0, StockReplenishmentUrgency.Low)]
    [InlineData(100.001, 100.0, StockReplenishmentUrgency.Warning)]
    [InlineData(125.0, 100.0, StockReplenishmentUrgency.Warning)]
    [InlineData(125.001, 100.0, StockReplenishmentUrgency.Normal)]
    [InlineData(500.0, 100.0, StockReplenishmentUrgency.Normal)]
    [InlineData(1.0, 0.0, StockReplenishmentUrgency.Normal)]
    public void ResolveUrgency_BoundaryTransitions_ShouldMatchExpectedUrgency(
        decimal currentStock, decimal minimumStock, StockReplenishmentUrgency expected)
    {
        Assert.Equal(expected, StockReplenishmentCalculator.ResolveUrgency(currentStock, minimumStock));
    }

    [Fact]
    public void Consolidate_ShouldReturnZeroTotals_WhenListIsEmptyOrNull()
    {
        var nullResult = StockReplenishmentCalculator.Consolidate(null!);
        Assert.Empty(nullResult.Items);
        Assert.Equal(0, nullResult.TotalItemsNeedingReplenishment);
        Assert.Equal(0, nullResult.CriticalCount);
        Assert.Equal(0, nullResult.LowCount);
        Assert.Equal(0, nullResult.WarningCount);
        Assert.Equal(0m, nullResult.TotalEstimatedCost);

        var emptyResult = StockReplenishmentCalculator.Consolidate(new List<StockReplenishmentItemInput>());
        Assert.Empty(emptyResult.Items);
        Assert.Equal(0m, emptyResult.TotalEstimatedCost);
    }

    [Fact]
    public void Consolidate_ShouldCalculateAccurateTotals()
    {
        var inputs = new List<StockReplenishmentItemInput>
        {
            // Critical (negativo)
            new(Guid.NewGuid(), "Tomate Pelado", MeasureUnit.un, 1m, 12m, 12m, 10m, -2m),
            // Low (abaixo do mínimo)
            new(Guid.NewGuid(), "Muçarela", MeasureUnit.g, 1000m, 40m, 0.04m, 5000m, 2000m),
            // Warning (perto do mínimo: 10 <= 12 <= 12.5)
            new(Guid.NewGuid(), "Calabresa", MeasureUnit.kg, 1m, 25m, 25m, 10m, 12m),
            // Normal (confortável: 30 > 12.5)
            new(Guid.NewGuid(), "Orégano", MeasureUnit.g, 500m, 15m, 0.03m, 1000m, 2500m)
        };

        var summary = StockReplenishmentCalculator.Consolidate(inputs);

        Assert.Equal(4, summary.Items.Count);
        Assert.Equal(1, summary.CriticalCount);
        Assert.Equal(1, summary.LowCount);
        Assert.Equal(1, summary.WarningCount);
        // Itens precisando de atenção/reposição: Tomate Pelado, Muçarela e Calabresa = 3
        Assert.Equal(3, summary.TotalItemsNeedingReplenishment);

        // Tomate: deficit 12 un, 12 pacotes * 12 = 144.00
        // Muçarela: deficit 3000g, 3 pacotes * 40 = 120.00
        // Calabresa: deficit 0kg, 0 pacotes = 0.00
        // Orégano: deficit 0g, 0 pacotes = 0.00
        // Total = 144 + 120 = 264.00
        Assert.Equal(264m, summary.TotalEstimatedCost);
    }
}

using PizzariaLucrativa.Domain.Enums;
using PizzariaLucrativa.Domain.Exceptions;
using PizzariaLucrativa.Domain.Services;

namespace PizzariaLucrativa.Domain.Tests;

/// <summary>
/// Docs/10, Sprints 4 e 6: testes unitários para o cálculo do total do pedido
/// (itens + taxa − desconto com arredondamento) e para a consolidação do lucro real
/// no período (faturamento − custo − taxa estimada de cartão + rateio fixo separado).
/// </summary>
public sealed class ProfitAndOrderTotalsTests
{
    [Fact]
    public void CalculateOrderTotals_DeliveryWithDiscount_RoundsCorrectly()
    {
        var items = new (int Quantity, decimal UnitPrice)[]
        {
            (2, 39.90m),
            (1, 12.50m)
        };

        var (subtotal, deliveryFee, discount, total) = ProfitReportCalculator.CalculateOrderTotals(
            items,
            OrderType.Delivery,
            configuredDeliveryFee: 8.00m,
            discount: 5.30m);

        Assert.Equal(92.30m, subtotal);
        Assert.Equal(8.00m, deliveryFee);
        Assert.Equal(5.30m, discount);
        Assert.Equal(95.00m, total);
    }

    [Fact]
    public void CalculateOrderTotals_CounterOrder_IgnoresConfiguredDeliveryFee()
    {
        var items = new (int Quantity, decimal UnitPrice)[] { (1, 45.00m) };

        var (subtotal, deliveryFee, discount, total) = ProfitReportCalculator.CalculateOrderTotals(
            items,
            OrderType.Counter,
            configuredDeliveryFee: 10.00m,
            discount: 0m);

        Assert.Equal(45.00m, subtotal);
        Assert.Equal(0m, deliveryFee);
        Assert.Equal(0m, discount);
        Assert.Equal(45.00m, total);
    }

    [Theory]
    [InlineData(50.00)]
    [InlineData(60.00)]
    public void CalculateOrderTotals_DiscountGreaterOrEqualToSubtotal_ThrowsDomainException(decimal invalidDiscount)
    {
        var items = new (int Quantity, decimal UnitPrice)[] { (1, 50.00m) };

        Assert.Throws<DomainException>(() =>
            ProfitReportCalculator.CalculateOrderTotals(
                items, OrderType.Pickup, configuredDeliveryFee: 0m, discount: invalidDiscount));
    }

    [Fact]
    public void Consolidate_ExcludesCancelledOrders_EstimatesCardFee_AndComputesFixedCostLineSeparately()
    {
        var entries = new[]
        {
            new OrderProfitEntry(
                Guid.NewGuid(), OrderStatus.Completed, OrderType.Delivery, false,
                PaymentMethod.Card, DateTime.UtcNow, Total: 100.00m, CostSnapshotTotal: 35.00m),
            new OrderProfitEntry(
                Guid.NewGuid(), OrderStatus.Completed, OrderType.Counter, false,
                PaymentMethod.Pix, DateTime.UtcNow, Total: 50.00m, CostSnapshotTotal: 15.00m),
            new OrderProfitEntry(
                Guid.NewGuid(), OrderStatus.Cancelled, OrderType.Delivery, true,
                PaymentMethod.Card, DateTime.UtcNow, Total: 80.00m, CostSnapshotTotal: 28.00m),
            new OrderProfitEntry(
                Guid.NewGuid(), OrderStatus.InPreparation, OrderType.Pickup, false,
                PaymentMethod.Cash, DateTime.UtcNow, Total: 40.00m, CostSnapshotTotal: 14.00m)
        };

        var summary = ProfitReportCalculator.Consolidate(
            entries,
            stockOutNetCost: 50.00m,
            cardFeePercentage: 3.00m,
            monthlyFixedCostTotal: 3000.00m,
            periodDays: 15);

        Assert.Equal(2, summary.CompletedOrdersCount);
        Assert.Equal(1, summary.CancelledOrdersCount);
        Assert.Equal(150.00m, summary.Revenue);
        Assert.Equal(50.00m, summary.IngredientCost);
        Assert.False(summary.HasCostDivergence);
        Assert.Equal(3.00m, summary.EstimatedCardFee); // 3% apenas sobre R$ 100 no cartão
        Assert.Equal(97.00m, summary.GrossProfit);     // 150 − 50 − 3
        Assert.Equal(64.67m, summary.GrossMarginPercentage);
        Assert.Equal(75.00m, summary.AverageTicket);

        // Premissa P9: rateio de 15/30 dias de R$ 3.000 = R$ 1.500 em linha separada
        Assert.Equal(1500.00m, summary.ProportionalFixedCost);
        Assert.Equal(-1403.00m, summary.NetResultAfterFixedCosts);
    }

    [Fact]
    public void Consolidate_ZeroOrders_DoesNotDivideByZero()
    {
        var summary = ProfitReportCalculator.Consolidate(
            Array.Empty<OrderProfitEntry>(),
            stockOutNetCost: 0m,
            cardFeePercentage: 2.99m,
            monthlyFixedCostTotal: 1200m,
            periodDays: 30);

        Assert.Equal(0, summary.CompletedOrdersCount);
        Assert.Equal(0m, summary.Revenue);
        Assert.Equal(0m, summary.IngredientCost);
        Assert.Equal(0m, summary.GrossProfit);
        Assert.Equal(0m, summary.GrossMarginPercentage);
        Assert.Equal(0m, summary.AverageTicket);
        Assert.False(summary.HasCostDivergence);
    }

    [Fact]
    public void Consolidate_DetectsDivergenceBetweenSnapshotAndStockMovements()
    {
        var entries = new[]
        {
            new OrderProfitEntry(
                Guid.NewGuid(), OrderStatus.Completed, OrderType.Counter, false,
                PaymentMethod.Cash, DateTime.UtcNow, Total: 60.00m, CostSnapshotTotal: 20.00m)
        };

        var summary = ProfitReportCalculator.Consolidate(
            entries,
            stockOutNetCost: 25.00m,
            cardFeePercentage: 0m,
            monthlyFixedCostTotal: 0m,
            periodDays: 1);

        Assert.True(summary.HasCostDivergence);
        Assert.Equal(5.00m, summary.CostDivergenceAmount);
    }
}

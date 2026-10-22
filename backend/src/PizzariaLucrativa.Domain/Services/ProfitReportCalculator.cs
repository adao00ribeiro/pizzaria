using PizzariaLucrativa.Domain.Enums;
using PizzariaLucrativa.Domain.Exceptions;

namespace PizzariaLucrativa.Domain.Services;

/// <summary>Entrada de pedido para consolidação de faturamento e lucro real (docs/10, Sprints 4 e 6).</summary>
public sealed record OrderProfitEntry(
    Guid OrderId,
    OrderStatus Status,
    OrderType Type,
    bool IsPublicSite,
    PaymentMethod? PaymentMethod,
    DateTime PlacedAt,
    decimal Total,
    decimal CostSnapshotTotal);

/// <summary>Resultado consolidado do relatório de lucro real no período (docs/10, Sprint 6).</summary>
public sealed record ProfitPeriodSummary(
    decimal Revenue,
    decimal IngredientCost,
    decimal StockOutCost,
    bool HasCostDivergence,
    decimal CostDivergenceAmount,
    decimal EstimatedCardFee,
    decimal GrossProfit,
    decimal GrossMarginPercentage,
    int CompletedOrdersCount,
    int CancelledOrdersCount,
    decimal AverageTicket,
    decimal MonthlyFixedCostTotal,
    decimal ProportionalFixedCost,
    decimal NetResultAfterFixedCosts);

/// <summary>
/// Cálculos puros de totalização de pedido (Sprint 4) e consolidação de lucro real
/// por período (Sprint 6 — premissas P6, P8 e P9).
/// </summary>
public static class ProfitReportCalculator
{
    /// <summary>Limite de tolerância para divergência entre snapshot da ficha e movimentos OrderOut.</summary>
    public const decimal DivergenceTolerance = 0.05m;

    /// <summary>
    /// Calcula os totais de um pedido (subtotal + taxa de entrega − desconto) com
    /// arredondamento monetário de 2 casas e validações de desconto (Sprint 4).
    /// </summary>
    public static (decimal Subtotal, decimal DeliveryFee, decimal Discount, decimal Total) CalculateOrderTotals(
        IEnumerable<(int Quantity, decimal UnitPrice)> items,
        OrderType type,
        decimal configuredDeliveryFee,
        decimal discount)
    {
        if (items is null)
            throw new DomainException("Informe os itens do pedido.");

        var itemList = items.ToList();
        if (itemList.Count == 0)
            throw new DomainException("Adicione ao menos um item ao pedido.");

        if (discount < 0)
            throw new DomainException("Desconto não pode ser negativo.");

        decimal subtotal = 0m;
        foreach (var (qty, unitPrice) in itemList)
        {
            if (qty <= 0)
                throw new DomainException("A quantidade do item deve ser maior que zero.");
            subtotal += Round2(unitPrice * qty);
        }

        subtotal = Round2(subtotal);
        var deliveryFee = type == OrderType.Delivery ? Round2(Math.Max(0m, configuredDeliveryFee)) : 0m;
        var roundedDiscount = Round2(discount);

        if (roundedDiscount > 0m && roundedDiscount >= subtotal)
            throw new DomainException("O desconto deve ser menor que o valor dos itens.");

        var total = Round2(subtotal + deliveryFee - roundedDiscount);
        return (subtotal, deliveryFee, roundedDiscount, total);
    }

    /// <summary>
    /// Consolida faturamento, custo de ingredientes, taxa estimada de cartão (P8),
    /// lucro bruto, margem, ticket médio e resultado após rateio de custo fixo (P9).
    /// Pedidos cancelados ou ainda em andamento ficam fora do faturamento e do custo.
    /// </summary>
    public static ProfitPeriodSummary Consolidate(
        IEnumerable<OrderProfitEntry> orders,
        decimal stockOutNetCost,
        decimal cardFeePercentage,
        decimal monthlyFixedCostTotal,
        int periodDays)
    {
        var list = (orders ?? Array.Empty<OrderProfitEntry>()).ToList();

        var completed = list.Where(o => o.Status == OrderStatus.Completed).ToList();
        var cancelledCount = list.Count(o => o.Status == OrderStatus.Cancelled);

        var revenue = Round2(completed.Sum(o => o.Total));
        var ingredientCost = Round2(completed.Sum(o => o.CostSnapshotTotal));
        var normalizedStockCost = Round2(Math.Max(0m, stockOutNetCost));

        var divergence = Round2(Math.Abs(ingredientCost - normalizedStockCost));
        var hasDivergence = divergence > DivergenceTolerance;

        var safeCardPct = Math.Max(0m, cardFeePercentage);
        var estimatedCardFee = Round2(
            completed
                .Where(o => o.PaymentMethod == PaymentMethod.Card)
                .Sum(o => Round2(o.Total * (safeCardPct / 100m))));

        // Premissa P6 + P8: lucro bruto = faturamento − custo real de ingredientes − taxa estimada de cartão.
        var grossProfit = Round2(revenue - ingredientCost - estimatedCardFee);
        var marginPct = revenue > 0m
            ? Round2((grossProfit / revenue) * 100m)
            : 0m;

        var averageTicket = completed.Count > 0
            ? Round2(revenue / completed.Count)
            : 0m;

        // Premissa P9: custo fixo em linha separada (proporcional aos dias do período, base 30 dias).
        var safeDays = Math.Max(1, periodDays);
        var safeMonthlyFixed = Round2(Math.Max(0m, monthlyFixedCostTotal));
        var proportionalFixed = Round2(safeMonthlyFixed * (safeDays / 30m));
        var netAfterFixed = Round2(grossProfit - proportionalFixed);

        return new ProfitPeriodSummary(
            Revenue: revenue,
            IngredientCost: ingredientCost,
            StockOutCost: normalizedStockCost,
            HasCostDivergence: hasDivergence,
            CostDivergenceAmount: divergence,
            EstimatedCardFee: estimatedCardFee,
            GrossProfit: grossProfit,
            GrossMarginPercentage: marginPct,
            CompletedOrdersCount: completed.Count,
            CancelledOrdersCount: cancelledCount,
            AverageTicket: averageTicket,
            MonthlyFixedCostTotal: safeMonthlyFixed,
            ProportionalFixedCost: proportionalFixed,
            NetResultAfterFixedCosts: netAfterFixed);
    }

    private static decimal Round2(decimal value)
        => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}

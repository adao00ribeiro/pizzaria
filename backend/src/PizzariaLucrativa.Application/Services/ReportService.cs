using Microsoft.EntityFrameworkCore;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Exceptions;
using PizzariaLucrativa.Application.Interfaces;
using PizzariaLucrativa.Domain.Entities;
using PizzariaLucrativa.Domain.Enums;
using PizzariaLucrativa.Domain.Services;

namespace PizzariaLucrativa.Application.Services;

/// <summary>
/// Relatório de lucro real (docs/10, Sprint 6 — restrito a <c>owner</c>):
/// faturamento de pedidos concluídos − custo real de ingredientes − taxa estimada de cartão (P8),
/// conferência contra baixas <see cref="StockMovementType.OrderOut"/> e linha separada de
/// resultado após rateio de custos fixos (P9).
/// </summary>
public sealed class ReportService : IReportService
{
    private readonly IAppDbContext _db;
    private readonly ITenantProvider _tenant;

    public ReportService(IAppDbContext db, ITenantProvider tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<ProfitReportDto> GetProfitSummaryAsync(
        DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var (startDate, endDate, endExclusive, days) = NormalizePeriod(from, to);

        var orders = await _db.ToListAsync(
            _db.Set<Order>().Where(o => o.PlacedAt >= startDate && o.PlacedAt < endExclusive), ct);

        var orderIds = orders.Select(o => o.Id).ToList();
        var items = orderIds.Count == 0
            ? new List<OrderItem>()
            : await _db.ToListAsync(
                _db.Set<OrderItem>().Where(i => orderIds.Contains(i.OrderId)), ct);

        var costByOrder = items
            .GroupBy(i => i.OrderId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.CostSnapshot * i.Quantity));

        var completedOrderIds = orders
            .Where(o => o.Status == OrderStatus.Completed)
            .Select(o => o.Id)
            .ToList();

        decimal stockOutNetCost = 0m;
        if (completedOrderIds.Count > 0)
        {
            var movements = await _db.ToListAsync(
                _db.Set<StockMovement>()
                    .Where(m => m.OrderId.HasValue && completedOrderIds.Contains(m.OrderId.Value)), ct);

            // Se nenhum movimento de estoque existir para os pedidos concluídos (ex.: pedido
            // concluído sem insumo estocável), usa o próprio snapshot como referência.
            if (movements.Count > 0)
            {
                var outSum = movements
                    .Where(m => m.Type == StockMovementType.OrderOut)
                    .Sum(m => m.Quantity * (m.UnitCost ?? 0m));
                var reversedSum = movements
                    .Where(m => m.Type == StockMovementType.In)
                    .Sum(m => m.Quantity * (m.UnitCost ?? 0m));
                stockOutNetCost = Math.Max(0m, outSum - reversedSum);
            }
            else
            {
                stockOutNetCost = completedOrderIds.Sum(id => costByOrder.GetValueOrDefault(id));
            }
        }

        var fees = await LoadFeesAsync(ct);
        var fixedCosts = await _db.ToListAsync(_db.Set<FixedCost>(), ct);
        var monthlyFixedTotal = fixedCosts.Sum(f => f.MonthlyValue);

        var entries = orders.Select(o => new OrderProfitEntry(
            OrderId: o.Id,
            Status: o.Status,
            Type: o.Type,
            IsPublicSite: o.PlacedByUserId is null,
            PaymentMethod: o.PaymentMethod,
            PlacedAt: o.PlacedAt,
            Total: o.Total,
            CostSnapshotTotal: costByOrder.GetValueOrDefault(o.Id))).ToList();

        var summary = ProfitReportCalculator.Consolidate(
            entries,
            stockOutNetCost,
            fees.CardFeePercentage,
            monthlyFixedTotal,
            days);

        return new ProfitReportDto(
            From: startDate,
            To: endDate,
            Revenue: summary.Revenue,
            IngredientCost: summary.IngredientCost,
            StockOutCost: summary.StockOutCost,
            HasCostDivergence: summary.HasCostDivergence,
            CostDivergenceAmount: summary.CostDivergenceAmount,
            EstimatedCardFee: summary.EstimatedCardFee,
            GrossProfit: summary.GrossProfit,
            GrossMarginPercentage: summary.GrossMarginPercentage,
            CompletedOrdersCount: summary.CompletedOrdersCount,
            CancelledOrdersCount: summary.CancelledOrdersCount,
            AverageTicket: summary.AverageTicket,
            MonthlyFixedCostTotal: summary.MonthlyFixedCostTotal,
            ProportionalFixedCost: summary.ProportionalFixedCost,
            NetResultAfterFixedCosts: summary.NetResultAfterFixedCosts);
    }

    public async Task<IReadOnlyList<ProfitByDayDto>> GetProfitByDayAsync(
        DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var (startDate, _, endExclusive, days) = NormalizePeriod(from, to);

        var completedOrders = await _db.ToListAsync(
            _db.Set<Order>()
                .Where(o => o.Status == OrderStatus.Completed
                            && o.PlacedAt >= startDate
                            && o.PlacedAt < endExclusive), ct);

        var orderIds = completedOrders.Select(o => o.Id).ToList();
        var items = orderIds.Count == 0
            ? new List<OrderItem>()
            : await _db.ToListAsync(
                _db.Set<OrderItem>().Where(i => orderIds.Contains(i.OrderId)), ct);

        var costByOrder = items
            .GroupBy(i => i.OrderId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.CostSnapshot * i.Quantity));

        var fees = await LoadFeesAsync(ct);
        var cardPct = Math.Max(0m, fees.CardFeePercentage);

        var byDayMap = completedOrders
            .GroupBy(o => o.PlacedAt.Date)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<ProfitByDayDto>(days);
        for (var i = 0; i < days; i++)
        {
            var day = startDate.AddDays(i);
            var dayOrders = byDayMap.GetValueOrDefault(day) ?? new List<Order>();

            var revenue = Round2(dayOrders.Sum(o => o.Total));
            var ingCost = Round2(dayOrders.Sum(o => costByOrder.GetValueOrDefault(o.Id)));
            var cardFee = Round2(dayOrders
                .Where(o => o.PaymentMethod == PaymentMethod.Card)
                .Sum(o => Round2(o.Total * (cardPct / 100m))));

            var totalCost = Round2(ingCost + cardFee);
            var profit = Round2(revenue - totalCost);
            var margin = revenue > 0m ? Round2((profit / revenue) * 100m) : 0m;

            result.Add(new ProfitByDayDto(
                Date: day.ToString("yyyy-MM-dd"),
                OrdersCount: dayOrders.Count,
                Revenue: revenue,
                Cost: ingCost,
                GrossProfit: profit,
                MarginPercentage: margin));
        }

        return result;
    }

    public async Task<IReadOnlyList<ProfitByProductDto>> GetProfitByProductAsync(
        DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var (startDate, _, endExclusive, _) = NormalizePeriod(from, to);

        var completedOrderIds = await _db.ToListAsync(
            _db.Set<Order>()
                .Where(o => o.Status == OrderStatus.Completed
                            && o.PlacedAt >= startDate
                            && o.PlacedAt < endExclusive)
                .Select(o => o.Id), ct);

        if (completedOrderIds.Count == 0)
            return Array.Empty<ProfitByProductDto>();

        var items = await _db.ToListAsync(
            _db.Set<OrderItem>().Where(i => completedOrderIds.Contains(i.OrderId)), ct);
        var itemIds = items.Select(i => i.Id).ToList();
        var flavors = itemIds.Count == 0
            ? new List<OrderItemFlavor>()
            : await _db.ToListAsync(
                _db.Set<OrderItemFlavor>().Where(f => itemIds.Contains(f.OrderItemId)), ct);

        var productIds = flavors.Select(f => f.ProductId).Distinct().ToList();
        var products = productIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await _db.ToListAsync(_db.Set<Product>().Where(p => productIds.Contains(p.Id)), ct))
                .ToDictionary(p => p.Id, p => p.Name);

        var comboIds = items.Where(i => i.ComboId.HasValue).Select(i => i.ComboId!.Value).Distinct().ToList();
        var combos = comboIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await _db.ToListAsync(_db.Set<Combo>().Where(c => comboIds.Contains(c.Id)), ct))
                .ToDictionary(c => c.Id, c => c.Name);

        var stats = new Dictionary<Guid, (string Name, decimal Qty, decimal Revenue, decimal Cost)>();

        foreach (var item in items)
        {
            var itemCostTotal = item.CostSnapshot * item.Quantity;

            if (item.ComboId is Guid comboId)
            {
                var name = combos.GetValueOrDefault(comboId, "Combo");
                var prev = stats.TryGetValue(comboId, out var existingCombo)
                    ? existingCombo
                    : (Name: name, Qty: 0m, Revenue: 0m, Cost: 0m);
                stats[comboId] = (name, prev.Qty + item.Quantity, prev.Revenue + item.Total, prev.Cost + itemCostTotal);
                continue;
            }

            var itemFlavors = flavors.Where(f => f.OrderItemId == item.Id).ToList();
            foreach (var flavor in itemFlavors)
            {
                var share = flavor.SharePercentage / 100m;
                var name = products.GetValueOrDefault(flavor.ProductId, "Produto");
                var prev = stats.TryGetValue(flavor.ProductId, out var existingProd)
                    ? existingProd
                    : (Name: name, Qty: 0m, Revenue: 0m, Cost: 0m);

                stats[flavor.ProductId] = (
                    name,
                    prev.Qty + (item.Quantity * share),
                    prev.Revenue + (item.Total * share),
                    prev.Cost + (itemCostTotal * share));
            }
        }

        return stats
            .Select(kv =>
            {
                var rev = Round2(kv.Value.Revenue);
                var cost = Round2(kv.Value.Cost);
                var profit = Round2(rev - cost);
                var margin = rev > 0m ? Round2((profit / rev) * 100m) : 0m;
                return new ProfitByProductDto(
                    ProductId: kv.Key,
                    ProductName: kv.Value.Name,
                    QuantitySold: Round2(kv.Value.Qty),
                    Revenue: rev,
                    Cost: cost,
                    Profit: profit,
                    MarginPercentage: margin);
            })
            .OrderByDescending(p => p.Profit)
            .ThenByDescending(p => p.Revenue)
            .ToList();
    }

    public async Task<IReadOnlyList<ProfitByChannelDto>> GetProfitByChannelAsync(
        DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var (startDate, _, endExclusive, _) = NormalizePeriod(from, to);

        var completedOrders = await _db.ToListAsync(
            _db.Set<Order>()
                .Where(o => o.Status == OrderStatus.Completed
                            && o.PlacedAt >= startDate
                            && o.PlacedAt < endExclusive), ct);

        var orderIds = completedOrders.Select(o => o.Id).ToList();
        var items = orderIds.Count == 0
            ? new List<OrderItem>()
            : await _db.ToListAsync(
                _db.Set<OrderItem>().Where(i => orderIds.Contains(i.OrderId)), ct);

        var costByOrder = items
            .GroupBy(i => i.OrderId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.CostSnapshot * i.Quantity));

        var totalRevenue = completedOrders.Sum(o => o.Total);

        static string ResolveChannel(Order o)
        {
            if (o.PlacedByUserId is null)
                return "Site público";

            return o.Type switch
            {
                OrderType.Delivery => "Entrega",
                OrderType.Pickup => "Retirada",
                _ => "Balcão"
            };
        }

        var channels = new[] { "Balcão", "Retirada", "Entrega", "Site público" };
        var grouped = completedOrders
            .GroupBy(ResolveChannel)
            .ToDictionary(g => g.Key, g => g.ToList());

        return channels
            .Select(ch =>
            {
                var list = grouped.GetValueOrDefault(ch) ?? new List<Order>();
                var rev = Round2(list.Sum(o => o.Total));
                var cost = Round2(list.Sum(o => costByOrder.GetValueOrDefault(o.Id)));
                var profit = Round2(rev - cost);
                var share = totalRevenue > 0m ? Round2((rev / totalRevenue) * 100m) : 0m;
                return new ProfitByChannelDto(ch, list.Count, rev, cost, profit, share);
            })
            .OrderByDescending(c => c.Revenue)
            .ToList();
    }

    private static (DateTime StartDate, DateTime EndDate, DateTime EndExclusive, int Days) NormalizePeriod(
        DateTime? from, DateTime? to)
    {
        var endDate = (to ?? DateTime.UtcNow).Date;
        var startDate = (from ?? endDate.AddDays(-29)).Date;

        if (startDate > endDate)
            throw new ValidationException("from", "A data inicial não pode ser posterior à data final.");

        var days = (int)(endDate - startDate).TotalDays + 1;
        if (days > 366)
            throw new ValidationException("period", "O período máximo de consulta é de 366 dias.");

        return (startDate, endDate, endDate.AddDays(1), days);
    }

    private async Task<FeeConfig> LoadFeesAsync(CancellationToken ct)
    {
        var tenantId = _tenant.GetTenantId();
        return await _db.FirstOrDefaultAsync(
                   _db.Set<FeeConfig>().Where(f => f.TenantId == tenantId), ct)
               ?? new FeeConfig { TenantId = tenantId };
    }

    private static decimal Round2(decimal value)
        => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}

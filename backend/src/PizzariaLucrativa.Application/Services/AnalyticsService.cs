using Microsoft.EntityFrameworkCore;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Interfaces;
using PizzariaLucrativa.Domain.Entities;
using PizzariaLucrativa.Domain.Enums;

namespace PizzariaLucrativa.Application.Services;

/// <summary>
/// Métricas de pedidos consumidas por analytics e pelas tools do assistente.
/// </summary>
public sealed class AnalyticsService : IAnalyticsService
{
    private readonly IAppDbContext _db;
    private readonly IReportService _reportService;

    public AnalyticsService(IAppDbContext db, IReportService reportService)
    {
        _db = db;
        _reportService = reportService;
    }

    public async Task<OrdersByRegionDto> GetOrdersByRegionAsync(
        AnalyticsPeriodDto period, int limit = 10, CancellationToken ct = default)
    {
        if (limit < 1) limit = 1;
        if (limit > 50) limit = 50;

        var orders = await _db.ToListAsync(
            _db.Set<Order>().Where(o => o.PlacedAt >= period.From && o.PlacedAt < period.To), ct);

        var nonCancelled = orders.Where(o => o.Status != OrderStatus.Cancelled).ToList();
        var totalOrders = nonCancelled.Count;
        var totalRevenue = nonCancelled.Where(o => o.Status == OrderStatus.Completed).Sum(o => o.Total);

        var groups = nonCancelled
            .GroupBy(o => new
            {
                NeighborhoodId = o.NeighborhoodId,
                District = string.IsNullOrWhiteSpace(o.AddressDistrict) ? "Não classificado" : o.AddressDistrict,
                Region = o.RegionSnapshot
            })
            .ToList();

        var items = groups
            .Select(g =>
            {
                var groupOrders = g.ToList();
                var ordersCount = groupOrders.Count;
                var completed = groupOrders.Where(o => o.Status == OrderStatus.Completed).ToList();
                var completedCount = completed.Count;
                var revenue = completed.Sum(o => o.Total);
                var avgTicket = completedCount > 0 ? revenue / completedCount : 0m;
                var share = totalOrders > 0 ? (decimal)ordersCount / totalOrders * 100m : 0m;

                return new OrdersByRegionItemDto(
                    g.Key.NeighborhoodId,
                    string.IsNullOrWhiteSpace(g.Key.District) ? "Não classificado" : g.Key.District,
                    g.Key.Region,
                    ordersCount,
                    completedCount,
                    decimal.Round(revenue, 2, MidpointRounding.AwayFromZero),
                    decimal.Round(avgTicket, 2, MidpointRounding.AwayFromZero),
                    decimal.Round(share, 2, MidpointRounding.AwayFromZero));
            })
            .OrderByDescending(i => i.OrdersCount)
            .Take(limit)
            .ToList();

        return new OrdersByRegionDto(period, items, totalOrders, decimal.Round(totalRevenue, 2, MidpointRounding.AwayFromZero));
    }

    public async Task<OrdersSummaryDto> GetOrdersSummaryAsync(
        AnalyticsPeriodDto period, CancellationToken ct = default)
    {
        var orders = await _db.ToListAsync(
            _db.Set<Order>().Where(o => o.PlacedAt >= period.From && o.PlacedAt < period.To), ct);

        var total = orders.Count;
        var received = orders.Count(o => o.Status == OrderStatus.Received);
        var inPrep = orders.Count(o => o.Status == OrderStatus.InPreparation);
        var ready = orders.Count(o => o.Status == OrderStatus.Ready);
        var outForDelivery = orders.Count(o => o.Status == OrderStatus.OutForDelivery);
        var completed = orders.Count(o => o.Status == OrderStatus.Completed);
        var cancelled = orders.Count(o => o.Status == OrderStatus.Cancelled);

        var counter = orders.Count(o => o.Type == OrderType.Counter);
        var pickup = orders.Count(o => o.Type == OrderType.Pickup);
        var delivery = orders.Count(o => o.Type == OrderType.Delivery);
        var publicSite = orders.Count(o => o.PlacedByUserId == null);

        var cash = orders.Count(o => o.PaymentMethod == PaymentMethod.Cash);
        var card = orders.Count(o => o.PaymentMethod == PaymentMethod.Card);
        var pix = orders.Count(o => o.PaymentMethod == PaymentMethod.Pix);
        var notInformed = orders.Count(o => o.PaymentMethod == null);

        var completedRevenue = orders.Where(o => o.Status == OrderStatus.Completed).Sum(o => o.Total);
        var avgTicket = completed > 0 ? completedRevenue / completed : 0m;

        return new OrdersSummaryDto(
            period,
            total,
            received,
            inPrep,
            ready,
            outForDelivery,
            completed,
            cancelled,
            counter,
            pickup,
            delivery,
            publicSite,
            cash,
            card,
            pix,
            notInformed,
            decimal.Round(completedRevenue, 2, MidpointRounding.AwayFromZero),
            decimal.Round(avgTicket, 2, MidpointRounding.AwayFromZero));
    }

    public async Task<SalesHeatmapDto> GetSalesHeatmapAsync(
        AnalyticsPeriodDto period, CancellationToken ct = default)
    {
        var orders = await _db.ToListAsync(
            _db.Set<Order>().Where(o => o.Status == OrderStatus.Completed && o.PlacedAt >= period.From && o.PlacedAt < period.To), ct);

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        var cells = new List<SalesHeatmapItemDto>();

        for (int dow = 0; dow < 7; dow++)
        {
            for (int hour = 0; hour < 24; hour++)
            {
                var dayOfWeek = (DayOfWeek)dow;
                var matching = orders.Where(o =>
                {
                    var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(o.PlacedAt, DateTimeKind.Utc), timeZone);
                    return local.DayOfWeek == dayOfWeek && local.Hour == hour;
                }).ToList();

                cells.Add(new SalesHeatmapItemDto(
                    dayOfWeek,
                    hour,
                    matching.Count,
                    decimal.Round(matching.Sum(o => o.Total), 2, MidpointRounding.AwayFromZero)));
            }
        }

        return new SalesHeatmapDto(period, cells);
    }

    public async Task<PeriodComparisonDto> ComparePeriodsAsync(
        AnalyticsPeriodDto periodA, AnalyticsPeriodDto periodB, CancellationToken ct = default)
    {
        var metricsA = await GetMetricsAsync(periodA, ct);
        var metricsB = await GetMetricsAsync(periodB, ct);

        var revenueChange = ComputeChange(metricsA.Revenue, metricsB.Revenue);
        var profitChange = ComputeChange(metricsA.GrossProfit, metricsB.GrossProfit);
        var ordersChange = ComputeChange(metricsA.OrdersCount, metricsB.OrdersCount);
        var ticketChange = ComputeChange(metricsA.AverageTicket, metricsB.AverageTicket);

        return new PeriodComparisonDto(
            periodA,
            periodB,
            metricsB,
            metricsA,
            new PeriodComparisonChangesDto(revenueChange, profitChange, ordersChange, ticketChange));
    }

    private async Task<PeriodMetricsDto> GetMetricsAsync(AnalyticsPeriodDto period, CancellationToken ct)
    {
        var report = await _reportService.GetProfitSummaryAsync(period.From, period.To, ct);
        return new PeriodMetricsDto(
            report.CompletedOrdersCount,
            report.Revenue,
            report.GrossProfit,
            report.GrossMarginPercentage,
            report.AverageTicket);
    }

    private static PeriodChangeDto ComputeChange(decimal current, decimal previous)
    {
        if (previous == 0m)
        {
            if (current == 0m)
                return new PeriodChangeDto(0m, 0m);
            return new PeriodChangeDto(current - previous, 100m);
        }
        var abs = current - previous;
        var pct = (abs / Math.Abs(previous)) * 100m;
        return new PeriodChangeDto(decimal.Round(abs, 2, MidpointRounding.AwayFromZero), decimal.Round(pct, 2, MidpointRounding.AwayFromZero));
    }
}

using Microsoft.EntityFrameworkCore;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Exceptions;
using PizzariaLucrativa.Application.Interfaces;
using PizzariaLucrativa.Domain.Entities;
using PizzariaLucrativa.Domain.Enums;
using PizzariaLucrativa.Domain.Services;
using FixedCostInput = PizzariaLucrativa.Domain.Services.FixedCostInput;

namespace PizzariaLucrativa.Application.Services;

/// <summary>Dashboard: resumo, rankings, detalhe individual e gráfico de margens (docs/07 §10).</summary>
public sealed class DashboardService : IDashboardService
{
    private readonly IAppDbContext _db;
    private readonly ITenantProvider _tenant;
    private readonly ITechnicalSheetService _technicalSheets;

    public DashboardService(
        IAppDbContext db,
        ITenantProvider tenant,
        ITechnicalSheetService technicalSheets)
    {
        _db = db;
        _tenant = tenant;
        _technicalSheets = technicalSheets;
    }

    public async Task<DashboardSummaryDto> SummaryAsync(CancellationToken ct = default)
    {
        var sheets = await _db.ToListAsync(
            _db.Set<TechnicalSheet>()
                .Select(s => new { s.MarginStatus, s.MarginPercentageReal }), ct);

        var fixedCosts = await _db.ToListAsync(_db.Set<FixedCost>(), ct);
        var allocation = PricingCalculator.AllocateFixedCosts(
            fixedCosts.Select(f => new FixedCostInput(
                f.MonthlyValue, f.AllocationType, f.ExpectedPizzasPerMonth, f.ExpectedMonthlyRevenue)));

        var openAlerts = await _db.CountAsync(
            _db.Set<Alert>().Where(a => a.Status == AlertStatus.Open), ct);

        var monthlyFixed = fixedCosts.Sum(f => f.MonthlyValue);
        var avgMargin = sheets.Count == 0
            ? 0
            : decimal.Round(sheets.Average(s => s.MarginPercentageReal), 2, MidpointRounding.AwayFromZero);

        return new DashboardSummaryDto(
            sheets.Count,
            sheets.Count(s => s.MarginStatus == MarginStatus.NeedsAdjustment),
            monthlyFixed,
            allocation.IndirectCostPerPizza,
            avgMargin,
            openAlerts);
    }

    public async Task<PagedResult<RankingItemDto>> RankingsAsync(
        string by, string? direction, PagedRequest page, CancellationToken ct = default)
    {
        var query = _db.Set<TechnicalSheet>()
            .Select(s => new
            {
                s.Product!.Name,
                Size = s.Size!.Name,
                s.EstimatedProfit,
                s.TotalCost,
                s.MarginStatus
            });

        var descending = !string.Equals(direction, "asc", StringComparison.OrdinalIgnoreCase);

        query = by.ToLowerInvariant() switch
        {
            "cost" => descending
                ? query.OrderByDescending(x => x.TotalCost).ThenBy(x => x.Name)
                : query.OrderBy(x => x.TotalCost).ThenBy(x => x.Name),
            _ => descending
                ? query.OrderByDescending(x => x.EstimatedProfit).ThenBy(x => x.Name)
                : query.OrderBy(x => x.EstimatedProfit).ThenBy(x => x.Name)
        };

        // by=profit|cost — valores para exibição.
        var total = await _db.CountAsync(query, ct);
        var rows = await _db.ToListAsync(
            query.Skip(page.Skip).Take(page.PageSize), ct);

        var useCost = string.Equals(by, "cost", StringComparison.OrdinalIgnoreCase);
        return new PagedResult<RankingItemDto>(
            rows.Select(r => new RankingItemDto(
                r.Name,
                r.Size,
                useCost ? r.TotalCost : r.EstimatedProfit,
                r.MarginStatus)).ToList(),
            total);
    }

    public async Task<TechnicalSheetDetailDto> IndividualAsync(
        Guid productId, Guid sizeId, CancellationToken ct = default)
    {
        var sheetId = await _db.FirstOrDefaultAsync(
            _db.Set<TechnicalSheet>()
                .Where(s => s.ProductId == productId && s.SizeId == sizeId)
                .Select(s => s.Id), ct);

        if (sheetId == Guid.Empty)
            throw new NotFoundException("Nenhuma ficha técnica encontrada para este produto e tamanho.");

        return await _technicalSheets.GetAsync(sheetId, ct);
    }

    public async Task<IReadOnlyList<MarginChartItemDto>> MarginsChartAsync(CancellationToken ct = default)
    {
        var sheets = await _db.ToListAsync(
            _db.Set<TechnicalSheet>()
                .OrderBy(s => s.MarginPercentageReal)
                .Select(s => new
                {
                    Name = s.Product!.Name + " " + s.Size!.Name,
                    s.MarginPercentageReal
                }), ct);

        return sheets
            .Select(s => new MarginChartItemDto(s.Name, s.MarginPercentageReal))
            .ToList();
    }
}

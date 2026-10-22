using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Exceptions;
using PizzariaLucrativa.Application.Interfaces;
using PizzariaLucrativa.Domain.Entities;
using PizzariaLucrativa.Domain.Enums;

namespace PizzariaLucrativa.Application.Services;

public sealed class FixedCostService : IFixedCostService
{
    private readonly IAppDbContext _db;
    private readonly ITenantProvider _tenant;
    private readonly IRecalculationQueue _recalculationQueue;

    public FixedCostService(IAppDbContext db, ITenantProvider tenant, IRecalculationQueue recalculationQueue)
    {
        _db = db;
        _tenant = tenant;
        _recalculationQueue = recalculationQueue;
    }

    public async Task<IReadOnlyList<FixedCostDto>> ListAsync(CancellationToken ct = default)
    {
        var items = await _db.ToListAsync(
            _db.Set<FixedCost>().OrderBy(f => f.Description), ct);
        return items.Select(Map).ToList();
    }

    public async Task<FixedCostDto> CreateAsync(FixedCostInput input, CancellationToken ct = default)
    {
        var cost = new FixedCost
        {
            TenantId = _tenant.GetTenantId(),
            Description = input.Description.Trim(),
            MonthlyValue = input.MonthlyValue,
            AllocationType = input.AllocationType,
            ExpectedPizzasPerMonth = input.ExpectedPizzasPerMonth,
            ExpectedMonthlyRevenue = input.ExpectedMonthlyRevenue
        };
        _db.Add(cost);
        await _db.SaveChangesAsync(ct);
        await _recalculationQueue.EnqueueAsync(cost.TenantId, ct);
        return Map(cost);
    }

    public async Task<FixedCostDto> UpdateAsync(Guid id, FixedCostInput input, CancellationToken ct = default)
    {
        var cost = await FindAsync(id, ct);
        cost.Description = input.Description.Trim();
        cost.MonthlyValue = input.MonthlyValue;
        cost.AllocationType = input.AllocationType;
        cost.ExpectedPizzasPerMonth = input.ExpectedPizzasPerMonth;
        cost.ExpectedMonthlyRevenue = input.ExpectedMonthlyRevenue;
        cost.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _recalculationQueue.EnqueueAsync(cost.TenantId, ct);
        return Map(cost);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var cost = await FindAsync(id, ct);
        _db.Remove(cost);
        await _db.SaveChangesAsync(ct);
        await _recalculationQueue.EnqueueAsync(cost.TenantId, ct);
    }

    private async Task<FixedCost> FindAsync(Guid id, CancellationToken ct)
    {
        var cost = await _db.FirstOrDefaultAsync(_db.Set<FixedCost>().Where(f => f.Id == id), ct);
        return cost ?? throw new NotFoundException("Custo fixo não encontrado.");
    }

    private static FixedCostDto Map(FixedCost cost)
    {
        var perPizza = cost.AllocationType == AllocationType.PerPizza && cost.ExpectedPizzasPerMonth is > 0
            ? cost.MonthlyValue / cost.ExpectedPizzasPerMonth.Value
            : 0;
        return new FixedCostDto(
            cost.Id, cost.Description, cost.MonthlyValue, cost.AllocationType,
            cost.ExpectedPizzasPerMonth, cost.ExpectedMonthlyRevenue, decimal.Round(perPizza, 4));
    }
}

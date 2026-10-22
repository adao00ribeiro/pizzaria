using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Interfaces;
using PizzariaLucrativa.Domain.Entities;

namespace PizzariaLucrativa.Application.Services;

public sealed class FeeService : IFeeService
{
    private readonly IAppDbContext _db;
    private readonly ITenantProvider _tenant;
    private readonly IRecalculationQueue _recalculationQueue;

    public FeeService(IAppDbContext db, ITenantProvider tenant, IRecalculationQueue recalculationQueue)
    {
        _db = db;
        _tenant = tenant;
        _recalculationQueue = recalculationQueue;
    }

    public async Task<FeeConfigDto> GetAsync(CancellationToken ct = default)
    {
        var tenantId = _tenant.GetTenantId();
        var config = await _db.FirstOrDefaultAsync(
            _db.Set<FeeConfig>().Where(f => f.TenantId == tenantId), ct);

        if (config is null)
        {
            config = new FeeConfig { TenantId = tenantId };
            _db.Add(config);
            await _db.SaveChangesAsync(ct);
        }

        return new FeeConfigDto(
            config.CardFeePercentage, config.DeliveryFeePercentage, config.DeliveryFixedValue,
            config.DefaultMarginPercentage, config.LossPercentageGlobal);
    }

    public async Task<FeeConfigDto> UpdateAsync(FeeConfigDto input, CancellationToken ct = default)
    {
        var tenantId = _tenant.GetTenantId();
        var config = await _db.FirstOrDefaultAsync(
            _db.Set<FeeConfig>().Where(f => f.TenantId == tenantId), ct);

        if (config is null)
        {
            config = new FeeConfig { TenantId = tenantId };
            _db.Add(config);
        }

        config.CardFeePercentage = input.CardFeePercentage;
        config.DeliveryFeePercentage = input.DeliveryFeePercentage;
        config.DeliveryFixedValue = input.DeliveryFixedValue;
        config.DefaultMarginPercentage = input.DefaultMarginPercentage;
        config.LossPercentageGlobal = input.LossPercentageGlobal;
        config.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await _recalculationQueue.EnqueueAsync(tenantId, ct);

        return input;
    }
}

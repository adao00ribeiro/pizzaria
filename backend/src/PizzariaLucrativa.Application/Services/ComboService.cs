using Microsoft.EntityFrameworkCore;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Exceptions;
using PizzariaLucrativa.Application.Interfaces;
using PizzariaLucrativa.Domain.Entities;
using PizzariaLucrativa.Domain.Enums;

namespace PizzariaLucrativa.Application.Services;

/// <summary>CRUD de combos com cálculo §9 (custo/preço somados com desconto, ou preço fixo).</summary>
public sealed class ComboService : IComboService
{
    private readonly IAppDbContext _db;
    private readonly ITenantProvider _tenant;
    private readonly IRecalculationQueue _recalculationQueue;

    public ComboService(IAppDbContext db, ITenantProvider tenant, IRecalculationQueue recalculationQueue)
    {
        _db = db;
        _tenant = tenant;
        _recalculationQueue = recalculationQueue;
    }

    public async Task<PagedResult<ComboDto>> ListAsync(PagedRequest page, CancellationToken ct = default)
    {
        var total = await _db.CountAsync(_db.Set<Combo>(), ct);
        var combos = await _db.ToListAsync(
            _db.Set<Combo>()
                .OrderBy(c => c.Name)
                .Skip(page.Skip)
                .Take(page.PageSize), ct);

        return new PagedResult<ComboDto>(await MapManyAsync(combos, ct), total);
    }

    public async Task<ComboDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var combo = await FindAsync(id, ct);
        var mapped = await MapManyAsync(new List<Combo> { combo }, ct);
        return mapped[0];
    }

    public async Task<ComboDto> CreateAsync(ComboInput input, CancellationToken ct = default)
    {
        var combo = new Combo
        {
            TenantId = _tenant.GetTenantId(),
            Name = input.Name.Trim(),
            DiscountValue = input.DiscountValue,
            FixedPrice = input.FixedPrice
        };
        ApplyItems(combo, input.Items);

        _db.Add(combo);
        await RecalculateAsync(combo, ct);
        await _db.SaveChangesAsync(ct);
        await _recalculationQueue.EnqueueAsync(combo.TenantId, ct);

        return await GetAsync(combo.Id, ct);
    }

    public async Task<ComboDto> UpdateAsync(Guid id, ComboInput input, CancellationToken ct = default)
    {
        var combo = await FindAsync(id, ct);
        combo.Name = input.Name.Trim();
        combo.DiscountValue = input.DiscountValue;
        combo.FixedPrice = input.FixedPrice;
        combo.UpdatedAt = DateTime.UtcNow;

        var existing = await _db.ToListAsync(
            _db.Set<ComboItem>().Where(ci => ci.ComboId == id), ct);
        _db.RemoveRange(existing);
        ApplyItems(combo, input.Items);

        await RecalculateAsync(combo, ct);
        await _db.SaveChangesAsync(ct);
        await _recalculationQueue.EnqueueAsync(combo.TenantId, ct);

        return await GetAsync(combo.Id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var combo = await FindAsync(id, ct);
        var items = await _db.ToListAsync(
            _db.Set<ComboItem>().Where(ci => ci.ComboId == id), ct);

        _db.RemoveRange(items);
        _db.Remove(combo);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<ComboDto> CalculateAsync(Guid id, CancellationToken ct = default)
    {
        var combo = await FindAsync(id, ct);
        await RecalculateAsync(combo, ct);
        await _db.SaveChangesAsync(ct);
        return await GetAsync(combo.Id, ct);
    }

    // ---------------------------------------------------------------- internals

    private void ApplyItems(Combo combo, IReadOnlyList<ComboItemInputDto> items)
    {
        var tenantId = _tenant.GetTenantId();
        combo.Items.Clear();
        foreach (var item in items)
        {
            combo.Items.Add(new ComboItem
            {
                TenantId = tenantId,
                ProductId = item.ProductId,
                SizeId = item.SizeId,
                Quantity = item.Quantity
            });
        }
    }

    private async Task RecalculateAsync(Combo combo, CancellationToken ct)
    {
        var tenantId = _tenant.GetTenantId();
        var fees = await _db.FirstOrDefaultAsync(
                       _db.Set<FeeConfig>().Where(f => f.TenantId == tenantId), ct)
                   ?? new FeeConfig { TenantId = tenantId };

        var inputs = combo.Items
            .Select(i => new ComboItemInputDto(i.ProductId, i.SizeId, i.Quantity))
            .ToList();
        var resolved = await ComboCalculator.ResolveItemsAsync(_db, inputs, ct);
        var result = ComboCalculator.Calculate(fees, resolved, combo.DiscountValue, combo.FixedPrice);

        combo.TotalCost = result.TotalCost;
        combo.TotalPrice = result.TotalPrice;
        combo.TaxValue = result.TaxValue;
        combo.Profit = result.Profit;
        combo.MarginPercentage = result.MarginPercentage;
        combo.MarginStatus = result.Status;
    }

    private async Task<Combo> FindAsync(Guid id, CancellationToken ct)
    {
        // Include obrigatório: RecalculateAsync e UpdateAsync operam sobre os itens do combo.
        var combo = await _db.FirstOrDefaultAsync(
            _db.Set<Combo>().Include(c => c.Items).Where(c => c.Id == id), ct);
        return combo ?? throw new NotFoundException("Combo não encontrado.");
    }

    private async Task<IReadOnlyList<ComboDto>> MapManyAsync(List<Combo> combos, CancellationToken ct)
    {
        if (combos.Count == 0)
            return Array.Empty<ComboDto>();

        var comboIds = combos.Select(c => c.Id).ToList();
        var items = await _db.ToListAsync(
            _db.Set<ComboItem>().Where(ci => comboIds.Contains(ci.ComboId)), ct);

        var productIds = items.Select(i => i.ProductId).Distinct().ToList();
        var sizeIds = items.Where(i => i.SizeId.HasValue).Select(i => i.SizeId!.Value).Distinct().ToList();

        var products = await _db.ToListAsync(
            _db.Set<Product>().Where(p => productIds.Contains(p.Id)), ct);
        var sizes = await _db.ToListAsync(
            _db.Set<Size>().Where(s => sizeIds.Contains(s.Id)), ct);

        return combos.Select(combo => new ComboDto(
            combo.Id,
            combo.Name,
            combo.DiscountValue,
            combo.FixedPrice,
            combo.TotalCost,
            combo.TotalPrice,
            combo.TaxValue,
            combo.Profit,
            combo.MarginPercentage,
            combo.MarginStatus,
            items.Where(ci => ci.ComboId == combo.Id)
                .Select(ci => new ComboItemDto(
                    ci.Id,
                    ci.ProductId,
                    products.FirstOrDefault(p => p.Id == ci.ProductId)?.Name ?? "Produto removido",
                    ci.SizeId,
                    ci.SizeId.HasValue
                        ? sizes.FirstOrDefault(s => s.Id == ci.SizeId)?.Name ?? "Tamanho removido"
                        : null,
                    ci.Quantity))
                .ToList())).ToList();
    }
}

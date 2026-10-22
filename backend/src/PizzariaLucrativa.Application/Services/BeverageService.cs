using Microsoft.EntityFrameworkCore;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Exceptions;
using PizzariaLucrativa.Application.Interfaces;
using PizzariaLucrativa.Domain.Entities;
using PizzariaLucrativa.Domain.Enums;
using PizzariaLucrativa.Domain.Exceptions;
using PizzariaLucrativa.Domain.Services;

namespace PizzariaLucrativa.Application.Services;

/// <summary>Bebidas: precificação simples por markup — sem ficha técnica nem rateio (§10).</summary>
public sealed class BeverageService : IBeverageService
{
    private readonly IAppDbContext _db;
    private readonly ITenantProvider _tenant;
    private readonly IRecalculationQueue _recalculationQueue;

    public BeverageService(IAppDbContext db, ITenantProvider tenant, IRecalculationQueue recalculationQueue)
    {
        _db = db;
        _tenant = tenant;
        _recalculationQueue = recalculationQueue;
    }

    public async Task<IReadOnlyList<BeverageDto>> ListAsync(CancellationToken ct = default)
    {
        var products = await _db.ToListAsync(
            _db.Set<Product>()
                .Where(p => p.Category == ProductCategory.Beverage)
                .OrderBy(p => p.Name), ct);

        var marginTarget = await DefaultMarginAsync(ct);
        return products.Select(p => Map(p, marginTarget)).ToList();
    }

    public async Task<BeverageDto> UpdateMarkupAsync(
        Guid id,
        decimal markupMultiplier,
        decimal? purchasePrice = null,
        CancellationToken ct = default)
    {
        if (markupMultiplier <= 0)
            throw new ValidationException("markupMultiplier", "Multiplicador de markup deve ser maior que zero.");

        if (purchasePrice is < 0)
            throw new ValidationException("purchasePrice", "Preço de compra não pode ser negativo.");

        var product = await _db.FirstOrDefaultAsync(
                _db.Set<Product>().Where(p => p.Id == id && p.Category == ProductCategory.Beverage), ct)
            ?? throw new NotFoundException("Bebida não encontrada.");

        if (purchasePrice.HasValue)
            product.PurchasePrice = purchasePrice.Value;

        product.MarkupMultiplier = markupMultiplier;
        product.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // Combos com esta bebida podem mudar de preço.
        var usedInCombo = await _db.Set<ComboItem>().AnyAsync(ci => ci.ProductId == id, ct);
        if (usedInCombo)
            await _recalculationQueue.EnqueueAsync(product.TenantId, ct);

        var marginTarget = await DefaultMarginAsync(ct);
        return Map(product, marginTarget);
    }

    // ---------------------------------------------------------------- internals

    private async Task<decimal> DefaultMarginAsync(CancellationToken ct)
    {
        var tenantId = _tenant.GetTenantId();
        var fees = await _db.FirstOrDefaultAsync(
            _db.Set<FeeConfig>().Where(f => f.TenantId == tenantId), ct);
        return fees?.DefaultMarginPercentage ?? 60;
    }

    private static BeverageDto Map(Product p, decimal marginTarget)
    {
        var purchase = p.PurchasePrice ?? 0;
        var markup = p.MarkupMultiplier ?? 2.0m;

        decimal price, margin;
        MarginStatus status;
        try
        {
            (price, margin, status) = PricingCalculator.Beverage(purchase, markup, marginTarget);
        }
        catch (DomainException)
        {
            price = 0;
            margin = 0;
            status = MarginStatus.NeedsAdjustment;
        }

        return new BeverageDto(p.Id, p.Name, purchase, markup, price, margin, status, p.ImageUrl);
    }
}

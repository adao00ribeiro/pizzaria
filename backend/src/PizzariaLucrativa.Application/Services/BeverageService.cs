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
                .Include(p => p.Ingredient)
                .Where(p => p.Category == ProductCategory.Beverage)
                .OrderBy(p => p.Name), ct);

        // 1. Contingência TPT / Include: para produtos com IngredientId mas cuja navegação veio nula, carrega em lote
        var missingNavIds = products
            .Where(p => p.Ingredient == null && p.IngredientId.HasValue)
            .Select(p => p.IngredientId!.Value)
            .Distinct()
            .ToList();

        if (missingNavIds.Count > 0)
        {
            var loadedIngredients = await _db.ToListAsync(
                _db.Set<Ingredient>().Where(i => missingNavIds.Contains(i.Id)), ct);
            var map = loadedIngredients.ToDictionary(i => i.Id);

            foreach (var p in products.Where(p => p.Ingredient == null && p.IngredientId.HasValue))
            {
                if (map.TryGetValue(p.IngredientId!.Value, out var ing))
                {
                    p.Ingredient = ing;
                }
            }
        }

        // 2. Se alguma bebida REALMENTE não tiver insumo vinculado (novo cadastro ou insumo ausente no banco), cria e vincula
        var trulyMissing = products.Where(p => p.Ingredient == null).ToList();
        if (trulyMissing.Count > 0)
        {
            var currentTenantId = _tenant.GetTenantId();
            foreach (var p in trulyMissing)
            {
                var tenantId = p.TenantId != Guid.Empty ? p.TenantId : currentTenantId;
                var ingredient = new Ingredient
                {
                    TenantId = tenantId,
                    Name = p.Name,
                    PurchaseUnit = MeasureUnit.un,
                    PurchaseQuantity = 1,
                    PurchasePrice = p.PurchasePrice ?? 0m,
                    UnitCost = p.PurchasePrice ?? 0m,
                    LossPercentage = 0,
                    CurrentStock = 0m,
                    MinimumStock = 0m,
                    MarkupMultiplier = p.MarkupMultiplier ?? 2.0m
                };
                _db.Add(ingredient);
                await _db.SaveChangesAsync(ct);
                p.IngredientId = ingredient.Id;
                p.Ingredient = ingredient;
            }
            await _db.SaveChangesAsync(ct);
        }

        var marginTarget = await DefaultMarginAsync(ct);
        return products.Select(p => Map(p, marginTarget)).ToList();
    }

    public async Task<BeverageDto> UpdateMarkupAsync(
        Guid id,
        decimal markupMultiplier,
        decimal? purchasePrice = null,
        decimal? currentStock = null,
        decimal? minimumStock = null,
        CancellationToken ct = default)
    {
        if (markupMultiplier <= 0)
            throw new ValidationException("markupMultiplier", "Multiplicador de markup deve ser maior que zero.");

        if (purchasePrice is < 0)
            throw new ValidationException("purchasePrice", "Preço de compra não pode ser negativo.");

        if (currentStock is < 0)
            throw new ValidationException("currentStock", "Quantidade em estoque não pode ser negativa.");

        if (minimumStock is < 0)
            throw new ValidationException("minimumStock", "Estoque mínimo não pode ser negativo.");

        var product = await _db.FirstOrDefaultAsync(
                _db.Set<Product>()
                    .Include(p => p.Ingredient)
                    .Where(p => p.Id == id && p.Category == ProductCategory.Beverage), ct)
            ?? throw new NotFoundException("Bebida não encontrada.");

        if (purchasePrice.HasValue)
            product.PurchasePrice = purchasePrice.Value;

        product.MarkupMultiplier = markupMultiplier;
        product.UpdatedAt = DateTime.UtcNow;

        if (product.Ingredient is null && product.IngredientId.HasValue)
        {
            product.Ingredient = await _db.FirstOrDefaultAsync(
                _db.Set<Ingredient>().Where(i => i.Id == product.IngredientId.Value), ct);
        }

        var currentTenantId = _tenant.GetTenantId();
        var tenantId = product.TenantId != Guid.Empty ? product.TenantId : currentTenantId;

        if (product.Ingredient is null)
        {
            var ingredient = new Ingredient
            {
                TenantId = tenantId,
                Name = product.Name,
                PurchaseUnit = MeasureUnit.un,
                PurchaseQuantity = 1,
                PurchasePrice = product.PurchasePrice ?? 0m,
                UnitCost = product.PurchasePrice ?? 0m,
                LossPercentage = 0,
                CurrentStock = currentStock ?? 0m,
                MinimumStock = minimumStock ?? 0m,
                MarkupMultiplier = markupMultiplier
            };
            _db.Add(ingredient);
            await _db.SaveChangesAsync(ct);
            product.IngredientId = ingredient.Id;
            product.Ingredient = ingredient;
        }
        else
        {
            product.Ingredient.Name = product.Name;
            if (purchasePrice.HasValue)
            {
                product.Ingredient.PurchasePrice = purchasePrice.Value;
                product.Ingredient.UnitCost = purchasePrice.Value;
            }
            product.Ingredient.MarkupMultiplier = markupMultiplier;
            if (currentStock.HasValue)
                product.Ingredient.CurrentStock = currentStock.Value;
            if (minimumStock.HasValue)
                product.Ingredient.MinimumStock = minimumStock.Value;
            product.Ingredient.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);

        // Dispara recálculo para sincronizar alertas e combos
        await _recalculationQueue.EnqueueAsync(tenantId, ct);

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
        var purchase = p.PurchasePrice ?? p.Ingredient?.PurchasePrice ?? p.Ingredient?.UnitCost ?? 0m;
        var markup = p.MarkupMultiplier ?? p.Ingredient?.MarkupMultiplier ?? 2.0m;

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

        var currentStock = p.Ingredient?.CurrentStock ?? 0m;
        var minimumStock = p.Ingredient?.MinimumStock ?? 0m;
        var belowMinimum = minimumStock > 0 && currentStock <= minimumStock;

        return new BeverageDto(
            p.Id, p.Name, purchase, markup, price, margin, status, p.ImageUrl,
            currentStock, minimumStock, belowMinimum);
    }
}

using Microsoft.EntityFrameworkCore;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Exceptions;
using PizzariaLucrativa.Application.Interfaces;
using PizzariaLucrativa.Domain.Entities;
using PizzariaLucrativa.Domain.Enums;

namespace PizzariaLucrativa.Application.Services;

public sealed class ProductService : IProductService
{
    private readonly IAppDbContext _db;
    private readonly ITenantProvider _tenant;

    public ProductService(IAppDbContext db, ITenantProvider tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<ProductDto>> ListAsync(ProductCategory? category, CancellationToken ct = default)
    {
        var query = _db.Set<Product>().AsQueryable();
        if (category.HasValue)
            query = query.Where(p => p.Category == category.Value);

        var products = await _db.ToListAsync(query.Include(p => p.Ingredient).OrderBy(p => p.Name), ct);
        if (products.Count == 0)
            return Array.Empty<ProductDto>();

        // Contingência TPT / Include: para produtos com IngredientId mas cuja navegação veio nula, carrega em lote
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

        return products.Select(p => Map(p)).ToList();
    }

    public async Task<ProductDto> CreateAsync(ProductInput input, CancellationToken ct = default)
    {
        var product = new Product
        {
            TenantId = _tenant.GetTenantId(),
            Name = input.Name.Trim(),
            Category = input.Category,
            IsActive = input.IsActive,
            ImageUrl = string.IsNullOrWhiteSpace(input.ImageUrl) ? null : input.ImageUrl.Trim()
        };
        ApplyBeveragePricing(product, input);
        await ApplyIngredientLinkAsync(product, input, ct);
        _db.Add(product);
        await _db.SaveChangesAsync(ct);
        return Map(product);
    }

    public async Task<ProductDto> UpdateAsync(Guid id, ProductInput input, CancellationToken ct = default)
    {
        var product = await FindAsync(id, ct);
        product.Name = input.Name.Trim();
        product.Category = input.Category;
        product.IsActive = input.IsActive;
        product.ImageUrl = string.IsNullOrWhiteSpace(input.ImageUrl) ? null : input.ImageUrl.Trim();
        ApplyBeveragePricing(product, input);
        await ApplyIngredientLinkAsync(product, input, ct);
        product.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Map(product);
    }

    private static void ApplyBeveragePricing(Product product, ProductInput input)
    {
        if (input.Category != ProductCategory.Beverage)
        {
            product.PurchasePrice = null;
            product.MarkupMultiplier = null;
            return;
        }

        if (input.PurchasePrice.HasValue)
            product.PurchasePrice = input.PurchasePrice.Value;

        if (input.MarkupMultiplier.HasValue)
            product.MarkupMultiplier = input.MarkupMultiplier.Value;
        else if (product.MarkupMultiplier is null or <= 0)
            product.MarkupMultiplier = 2.0m;
    }

    /// <summary>Vincula ou cria automaticamente um insumo de estoque para a bebida (docs/10, Sprint 2).</summary>
    private async Task ApplyIngredientLinkAsync(Product product, ProductInput input, CancellationToken ct)
    {
        if (input.Category != ProductCategory.Beverage)
        {
            product.IngredientId = null;
            return;
        }

        Ingredient? ingredient = null;
        if (input.IngredientId is Guid ingredientId)
        {
            ingredient = await _db.FirstOrDefaultAsync(
                _db.Set<Ingredient>().Where(i => i.Id == ingredientId), ct)
                ?? throw new ValidationException("ingredientId", "Ingrediente não encontrado.");
        }
        else if (product.IngredientId is Guid existingIngredientId)
        {
            ingredient = await _db.FirstOrDefaultAsync(
                _db.Set<Ingredient>().Where(i => i.Id == existingIngredientId), ct);
        }

        if (ingredient is null)
        {
            var currentTenantId = _tenant.GetTenantId();
            var tenantId = product.TenantId != Guid.Empty ? product.TenantId : currentTenantId;
            ingredient = new Ingredient
            {
                TenantId = tenantId,
                Name = product.Name,
                PurchaseUnit = MeasureUnit.un,
                PurchaseQuantity = 1,
                PurchasePrice = product.PurchasePrice ?? 0m,
                UnitCost = product.PurchasePrice ?? 0m,
                LossPercentage = 0,
                CurrentStock = input.CurrentStock ?? 0m,
                MinimumStock = input.MinimumStock ?? 0m,
                MarkupMultiplier = product.MarkupMultiplier ?? 2.0m
            };
            _db.Add(ingredient);
            await _db.SaveChangesAsync(ct);
        }
        else
        {
            ingredient.Name = product.Name;
            if (product.PurchasePrice.HasValue)
            {
                ingredient.PurchasePrice = product.PurchasePrice.Value;
                ingredient.UnitCost = product.PurchasePrice.Value;
            }
            if (product.MarkupMultiplier.HasValue)
                ingredient.MarkupMultiplier = product.MarkupMultiplier.Value;
            if (input.CurrentStock.HasValue)
                ingredient.CurrentStock = input.CurrentStock.Value;
            if (input.MinimumStock.HasValue)
                ingredient.MinimumStock = input.MinimumStock.Value;
            ingredient.UpdatedAt = DateTime.UtcNow;
        }

        product.IngredientId = ingredient.Id;
        product.Ingredient = ingredient;
        if (product.PurchasePrice is null or <= 0)
            product.PurchasePrice = ingredient.UnitCost;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var product = await FindAsync(id, ct);

        var hasSheets = await _db.Set<TechnicalSheet>().AnyAsync(s => s.ProductId == id);
        var hasCombos = await _db.Set<ComboItem>().AnyAsync(c => c.ProductId == id);

        if (hasSheets || hasCombos)
            throw new ConflictException(
                "Produto possui fichas técnicas ou combos. Exclua ou desvincule esses registros antes (RF-03).");

        _db.Remove(product);
        await _db.SaveChangesAsync(ct);
    }

    private async Task<Product> FindAsync(Guid id, CancellationToken ct)
    {
        var product = await _db.FirstOrDefaultAsync(
            _db.Set<Product>().Include(p => p.Ingredient).Where(p => p.Id == id), ct);
        if (product is null)
            throw new NotFoundException("Produto não encontrado.");

        if (product.Ingredient is null && product.IngredientId.HasValue)
        {
            product.Ingredient = await _db.FirstOrDefaultAsync(
                _db.Set<Ingredient>().Where(i => i.Id == product.IngredientId.Value), ct);
        }

        return product;
    }

    private static ProductDto Map(Product p)
    {
        if (p.Category == ProductCategory.Beverage)
        {
            var purchase = p.PurchasePrice ?? p.Ingredient?.PurchasePrice ?? p.Ingredient?.UnitCost ?? 0m;
            var markup = p.MarkupMultiplier ?? p.Ingredient?.MarkupMultiplier ?? 2.0m;
            var suggested = decimal.Round(purchase * markup, 2, MidpointRounding.AwayFromZero);
            var currentStock = p.Ingredient?.CurrentStock;
            var minimumStock = p.Ingredient?.MinimumStock;
            return new ProductDto(
                p.Id, p.Name, p.Category, p.IsActive, markup, suggested, p.IngredientId,
                purchase, p.ImageUrl, currentStock, minimumStock);
        }

        return new ProductDto(
            p.Id, p.Name, p.Category, p.IsActive, null, null, p.IngredientId,
            null, p.ImageUrl, null, null);
    }
}

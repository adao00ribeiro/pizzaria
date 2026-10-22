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

        var products = await _db.ToListAsync(query.OrderBy(p => p.Name), ct);
        if (products.Count == 0)
            return Array.Empty<ProductDto>();

        // Bebidas exibem markup e preço calculado (§10).
        var beverageIds = products.Where(p => p.Category == ProductCategory.Beverage).Select(p => p.Id).ToList();
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

    /// <summary>Vincula a bebida a um insumo para dar baixa de estoque (docs/10, Sprint 2).</summary>
    private async Task ApplyIngredientLinkAsync(Product product, ProductInput input, CancellationToken ct)
    {
        if (input.IngredientId is not Guid ingredientId)
        {
            product.IngredientId = null;
            return;
        }

        if (input.Category != ProductCategory.Beverage)
            throw new ValidationException("ingredientId",
                "Só bebidas podem ser vinculadas a um insumo de estoque.");

        var ingredient = await _db.FirstOrDefaultAsync(
            _db.Set<Ingredient>().Where(i => i.Id == ingredientId), ct);
        if (ingredient is null)
            throw new ValidationException("ingredientId", "Ingrediente não encontrado.");

        product.IngredientId = ingredient.Id;
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
        var product = await _db.FirstOrDefaultAsync(_db.Set<Product>().Where(p => p.Id == id), ct);
        return product ?? throw new NotFoundException("Produto não encontrado.");
    }

    private static ProductDto Map(Product p)
    {
        if (p.Category == ProductCategory.Beverage)
        {
            var purchase = p.PurchasePrice ?? 0m;
            var markup = p.MarkupMultiplier ?? 2.0m;
            var suggested = decimal.Round(purchase * markup, 2, MidpointRounding.AwayFromZero);
            return new ProductDto(
                p.Id, p.Name, p.Category, p.IsActive, markup, suggested, p.IngredientId, p.PurchasePrice, p.ImageUrl);
        }

        return new ProductDto(
            p.Id, p.Name, p.Category, p.IsActive, null, null, p.IngredientId, null, p.ImageUrl);
    }
}

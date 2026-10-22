using Microsoft.EntityFrameworkCore;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Exceptions;
using PizzariaLucrativa.Application.Interfaces;
using PizzariaLucrativa.Domain.Entities;
using PizzariaLucrativa.Domain.Enums;
using PizzariaLucrativa.Domain.Exceptions;
using PizzariaLucrativa.Domain.Services;

namespace PizzariaLucrativa.Application.Services;

/// <summary>Cálculos de precificação on-demand: mistas e prévia de combo (§8/§9).</summary>
public sealed class PricingService : IPricingService
{
    private readonly IAppDbContext _db;
    private readonly ITenantProvider _tenant;

    public PricingService(IAppDbContext db, ITenantProvider tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<MistaResultDto> MistaAsync(HalfHalfInput input, int expectedFlavors, CancellationToken ct = default)
    {
        if (input.ProductIds.Count != expectedFlavors)
        {
            throw new ValidationException("productIds", expectedFlavors == 2
                ? "Selecione exatamente 2 sabores para o meio a meio."
                : "Selecione exatamente 4 sabores.");
        }

        var productIds = input.ProductIds.ToList();

        var sheets = await _db.ToListAsync(
            _db.Set<TechnicalSheet>()
                .Where(s => s.SizeId == input.SizeId && productIds.Contains(s.ProductId)), ct);

        var missing = productIds
            .Where(pid => sheets.All(s => s.ProductId != pid))
            .ToList();
        if (missing.Count > 0)
        {
            throw new ValidationException(
                "productIds",
                "Todos os sabores precisam de ficha técnica para o tamanho escolhido (docs/06 §8).");
        }

        var sheetIds = sheets.Select(s => s.Id).ToList();
        var sheetItems = await _db.ToListAsync(
            _db.Set<TechnicalSheetItem>().Where(ti => sheetIds.Contains(ti.TechnicalSheetId)), ct);
        var products = await _db.ToListAsync(
            _db.Set<Product>().Where(p => productIds.Contains(p.Id)), ct);

        var (fees, allocation) = await PricingInputs.BuildTenantContextAsync(_db, _tenant, ct);
        var ctx = new CalculationContext(_db);
        await ctx.LoadAsync(ct);

        var flavorResults = new List<(string Name, PricingResult Result)>();
        foreach (var productId in productIds)
        {
            var sheet = sheets.First(s => s.ProductId == productId);
            var items = sheetItems.Where(ti => ti.TechnicalSheetId == sheet.Id).ToList();

            if (items.Count == 0)
            {
                var name = products.FirstOrDefault(p => p.Id == productId)?.Name ?? "Sabor";
                throw new ValidationException("productIds", $"O sabor '{name}' não possui itens na ficha técnica.");
            }

            var pricingInput = PricingInputs.Build(
                fees,
                allocation,
                items.Select(ti => new SheetItemInput(
                    ti.Type, ti.IngredientId, ti.PreparationId, ti.Quantity, ti.Unit)).ToList(),
                sheet.PackagingCost,
                sheet.LossPercentage,
                sheet.MarginPercentage);

            var productName = products.FirstOrDefault(p => p.Id == productId)?.Name ?? "Sabor";
            flavorResults.Add((productName, Calculate(pricingInput, ctx)));
        }

        var flavors = flavorResults
            .Select(f => (f.Name, f.Result.TotalCost, f.Result.SuggestedPrice))
            .ToList();

        var mista = PricingCalculator.Mista(
            flavors,
            fees.CardFeePercentage + fees.DeliveryFeePercentage,
            fees.DefaultMarginPercentage);

        var avgDirect = flavorResults.Average(f => f.Result.DirectCost);
        var avgIndirect = flavorResults.Average(f => f.Result.IndirectCost);

        return new MistaResultDto(
            Round4(avgDirect),
            Round4(avgIndirect),
            mista.Cost,
            mista.Price,
            PricingCalculator.CardPrice(mista.Price),
            mista.TaxValue,
            mista.Profit,
            mista.MarginPercentage,
            fees.DefaultMarginPercentage,
            mista.Status,
            flavorResults
                .Select(f => new BreakdownItemDto(f.Name, f.Result.TotalCost))
                .ToList(),
            mista.Flavors.Select(f => new MistaFlavorDto(f.Name, f.Cost, f.Price)).ToList());
    }

    public async Task<ComboResultDto> ComboPreviewAsync(ComboInput input, CancellationToken ct = default)
    {
        var fees = await LoadFeesAsync(ct);
        var items = await ComboCalculator.ResolveItemsAsync(_db, input.Items, ct);
        return ComboCalculator.Calculate(fees, items, input.DiscountValue, input.FixedPrice).ToDto();
    }

    // ---------------------------------------------------------------- internals

    private async Task<Domain.Entities.FeeConfig> LoadFeesAsync(CancellationToken ct)
    {
        var tenantId = _tenant.GetTenantId();
        return await _db.FirstOrDefaultAsync(
                   _db.Set<FeeConfig>().Where(f => f.TenantId == tenantId), ct)
               ?? new FeeConfig { TenantId = tenantId };
    }

    private static PricingResult Calculate(SheetPricingInput input, ICalculationSource ctx)
    {
        try
        {
            return PricingCalculator.PriceSheet(input, ctx);
        }
        catch (DomainException ex)
        {
            throw new ValidationException("productIds", ex.Message);
        }
    }

    private static decimal Round4(decimal value)
        => decimal.Round(value, 4, MidpointRounding.AwayFromZero);
}

internal static class ComboResultExtensions
{
    public static ComboResultDto ToDto(this ComboResult result) => new(
        result.TotalCost, result.TotalPrice, result.TaxValue,
        result.Profit, result.MarginPercentage, result.Status);
}

/// <summary>
/// Resolve custo/preço por item de combo (pizzas pela ficha técnica, bebidas pelo
/// markup — docs/06 §9/§10) e executa o cálculo §9. Compartilhado por
/// ComboService, PricingService (preview) e RecalculationService.
/// </summary>
internal static class ComboCalculator
{
    public static async Task<IReadOnlyList<ComboItemInput>> ResolveItemsAsync(
        IAppDbContext db,
        IReadOnlyList<ComboItemInputDto> items,
        CancellationToken ct)
    {
        var resolved = new List<ComboItemInput>();

        foreach (var item in items)
        {
            var product = await db.FirstOrDefaultAsync(
                    db.Set<Product>().Where(p => p.Id == item.ProductId), ct)
                ?? throw new ValidationException("items", "Produto do combo não encontrado.");

            if (item.SizeId is Guid sizeId)
            {
                var sheet = await db.FirstOrDefaultAsync(
                        db.Set<TechnicalSheet>()
                            .Where(s => s.ProductId == item.ProductId && s.SizeId == sizeId), ct)
                    ?? throw new ValidationException("items",
                        $"Produto '{product.Name}' não possui ficha técnica para o tamanho selecionado.");

                resolved.Add(new ComboItemInput(
                    product.Id, sizeId, item.Quantity, sheet.TotalCost, sheet.SuggestedPrice));
            }
            else
            {
                if (product.Category != ProductCategory.Beverage)
                    throw new ValidationException("items",
                        $"Selecione o tamanho do produto '{product.Name}'.");

                var purchase = product.PurchasePrice ?? 0;
                var markup = product.MarkupMultiplier ?? 2.0m;
                var price = decimal.Round(purchase * markup, 2, MidpointRounding.AwayFromZero);
                resolved.Add(new ComboItemInput(product.Id, null, item.Quantity, purchase, price));
            }
        }

        return resolved;
    }

    public static ComboResult Calculate(
        Domain.Entities.FeeConfig fees,
        IReadOnlyList<ComboItemInput> items,
        decimal discountValue,
        decimal? fixedPrice)
    {
        try
        {
            return PricingCalculator.Combo(new ComboPricingInput(
                items,
                discountValue,
                fixedPrice,
                fees.CardFeePercentage,
                fees.DeliveryFeePercentage,
                fees.DefaultMarginPercentage));
        }
        catch (DomainException ex)
        {
            throw new ValidationException("items", ex.Message);
        }
    }
}

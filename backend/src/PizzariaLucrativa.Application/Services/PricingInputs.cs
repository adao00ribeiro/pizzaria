using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Interfaces;
using PizzariaLucrativa.Domain.Entities;
using PizzariaLucrativa.Domain.Enums;
using PizzariaLucrativa.Domain.Services;
using FixedCostInput = PizzariaLucrativa.Domain.Services.FixedCostInput;

namespace PizzariaLucrativa.Application.Services;

/// <summary>
/// Monta a entrada do motor de cálculo a partir do estado do tenant
/// (taxas + rateio de custos fixos) — insumo comum de fichas, previews e mistas.
/// </summary>
internal static class PricingInputs
{
    public static async Task<(FeeConfig Fees, FixedCostAllocation Allocation)> BuildTenantContextAsync(
        IAppDbContext db, ITenantProvider tenant, CancellationToken ct)
    {
        var tenantId = tenant.GetTenantId();

        var fees = await db.FirstOrDefaultAsync(
            db.Set<FeeConfig>().Where(f => f.TenantId == tenantId), ct)
            ?? new FeeConfig { TenantId = tenantId };

        var fixedCosts = await db.ToListAsync(db.Set<FixedCost>(), ct);

        var allocation = PricingCalculator.AllocateFixedCosts(
            fixedCosts.Select(f => new FixedCostInput(
                f.MonthlyValue, f.AllocationType, f.ExpectedPizzasPerMonth, f.ExpectedMonthlyRevenue)));

        return (fees, allocation);
    }

    public static SheetPricingInput Build(
        FeeConfig fees,
        FixedCostAllocation allocation,
        IReadOnlyList<SheetItemInput> items,
        decimal packagingCost,
        decimal? lossPercentage,
        decimal? marginPercentage)
        => new(
            items.Select(MapItem).ToList(),
            packagingCost,
            lossPercentage,
            marginPercentage,
            fees.DefaultMarginPercentage,
            fees.CardFeePercentage,
            fees.DeliveryFeePercentage,
            allocation,
            fees.LossPercentageGlobal);

    private static SheetItemData MapItem(SheetItemInput item)
    {
        var id = item.Type == SheetItemType.Ingredient
            ? item.IngredientId ?? Guid.Empty
            : item.PreparationId ?? Guid.Empty;
        return new SheetItemData(
            string.Empty, item.Type, item.IngredientId ?? Guid.Empty, item.PreparationId ?? Guid.Empty,
            item.Quantity, item.Unit) with { };
    }

    public static PricingResultDto Map(PricingResult result) => new(
        result.DirectCost, result.IndirectCost, result.TotalCost, result.SuggestedPrice,
        result.CardPrice, result.TaxValue, result.EstimatedProfit, result.MarginRealPercentage,
        result.MarginTarget, result.Status,
        result.Breakdown.Select(b => new BreakdownItemDto(b.Label, b.Cost)).ToList());
}

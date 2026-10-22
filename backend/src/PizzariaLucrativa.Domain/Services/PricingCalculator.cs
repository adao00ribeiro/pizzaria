using PizzariaLucrativa.Domain.Enums;
using PizzariaLucrativa.Domain.Exceptions;

namespace PizzariaLucrativa.Domain.Services;

/// <summary>
/// Motor de cálculo de precificação — função pura de domínio (NF-R01), sem
/// dependência de framework ou banco. Fórmulas: docs/06 — Regras de Negócio.
///
/// Política de arredondamento (NF-R05/R06):
/// - Custos unitários são razões (R$/g) e ficam com precisão total;
/// - Linhas monetárias (custo de item, custo total) são arredondadas a 4 casas;
/// - Preços finais são arredondados a 2 casas (dinheiro cobrado do cliente);
/// - Lucro/margem são derivados do preço já arredondado — a tolerância de
///   0,01 p.p. da margem absorve o resíduo de arredondamento.
/// </summary>
public static class PricingCalculator
{
    /// <summary>Máximo de aninhamento de preparos (docs/06 §2).</summary>
    public const int MaxPreparationDepth = 5;

    /// <summary>Tolerância de comparação da margem real × alvo, em pontos percentuais (docs/06 §6).</summary>
    public const decimal MarginTolerance = 0.01m;

    private static decimal Round4(decimal value)
        => decimal.Round(value, 4, MidpointRounding.AwayFromZero);

    private static decimal Round2(decimal value)
        => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    // ---------------------------------------------------------------- §1 insumo

    /// <summary>
    /// Custo por unidade de compra do ingrediente (docs/06 §1):
    /// UnitCost = (PurchasePrice ÷ PurchaseQuantity) × (1 + LossPercentage/100).
    /// Expresso por unidade de compra (1 g, 1 ml, 1 un...); converta na fronteira
    /// com <see cref="UnitCostPerUseUnit"/>.
    /// </summary>
    public static decimal IngredientUnitCost(IngredientData ingredient)
    {
        if (ingredient.PurchaseQuantity <= 0)
            throw new DomainException($"Ingrediente '{ingredient.Name}': quantidade de compra deve ser maior que zero.");

        return (ingredient.PurchasePrice / ingredient.PurchaseQuantity)
               * (1 + ingredient.LossPercentage / 100);
    }

    /// <summary>
    /// Custo unitário já convertido para a unidade de uso (ex.: comprado em kg,
    /// uso em g → ×0,001 — o custo por grama é o custo por kg dividido por 1000).
    /// </summary>
    public static decimal UnitCostPerUseUnit(IngredientData ingredient, MeasureUnit useUnit)
        => IngredientUnitCost(ingredient) * UnitConverter.Factor(useUnit, ingredient.PurchaseUnit);

    /// <summary>Custo total do uso de uma quantidade do ingrediente em uma unidade.</summary>
    public static decimal IngredientUseCost(IngredientData ingredient, decimal quantity, MeasureUnit useUnit)
        => Round4(UnitCostPerUseUnit(ingredient, useUnit) * quantity);

    // ---------------------------------------------------------------- §2 preparo

    /// <summary>
    /// Custo total e por unidade de rendimento de um preparo, resolvendo preparos
    /// aninhados recursivamente com detecção de ciclos e limite de profundidade.
    /// </summary>
    public static PreparationCostResult PreparationCost(
        PreparationData preparation,
        ICalculationSource source,
        decimal globalLossPercentage = 0)
        => PreparationCost(preparation, source, new List<Guid>(), globalLossPercentage);

    private static PreparationCostResult PreparationCost(
        PreparationData preparation,
        ICalculationSource source,
        List<Guid> trail,
        decimal globalLossPercentage)
    {
        if (trail.Contains(preparation.Id))
        {
            var cycle = trail.Skip(trail.IndexOf(preparation.Id)).Append(preparation.Id).ToList();
            throw new CycleDetectedException(cycle);
        }

        if (trail.Count >= MaxPreparationDepth)
            throw new DomainException(
                $"Preparo '{preparation.Name}' excede o limite de {MaxPreparationDepth} níveis de aninhamento.");

        if (preparation.YieldQuantity <= 0)
            throw new DomainException($"Preparo '{preparation.Name}': rendimento deve ser maior que zero.");

        trail.Add(preparation.Id);
        try
        {
            decimal total = 0;
            foreach (var item in preparation.Items)
            {
                decimal unitCost;
                if (item.Type == SheetItemType.Ingredient)
                {
                    var ingredient = item.IngredientId is Guid iid ? source.GetIngredient(iid) : null;
                    if (ingredient is null)
                        throw new DomainException($"Preparo '{preparation.Name}': ingrediente do item não encontrado.");
                    unitCost = UnitCostPerUseUnit(ingredient, item.Unit);
                }
                else
                {
                    var child = item.SourcePreparationId is Guid pid ? source.GetPreparation(pid) : null;
                    if (child is null)
                        throw new DomainException($"Preparo '{preparation.Name}': preparo aninhado do item não encontrado.");
                    var childResult = PreparationCost(child, source, trail, globalLossPercentage);
                    unitCost = PreparationYieldUnitCost(child, childResult.TotalCost, item.Unit);
                }

                total += unitCost * item.Quantity;
            }

            // Perda global do tenant incide sobre o custo do preparo (opcional).
            total *= 1 + globalLossPercentage / 100;

            return new PreparationCostResult(Round4(total), total / preparation.YieldQuantity);
        }
        finally
        {
            trail.RemoveAt(trail.Count - 1);
        }
    }

    /// <summary>Custo por unidade de uso de um preparo, convertendo da unidade de rendimento se necessário.</summary>
    private static decimal PreparationYieldUnitCost(PreparationData preparation, decimal totalCost, MeasureUnit useUnit)
        => totalCost / preparation.YieldQuantity * UnitConverter.Factor(useUnit, preparation.YieldUnit);

    // ---------------------------------------------------------------- §3 custo direto

    /// <summary>
    /// Custo direto da ficha (docs/06 §3):
    /// (Σ itens + embalagem) × (1 + perda adicional da ficha/100).
    /// </summary>
    public static (decimal DirectCost, IReadOnlyList<BreakdownItem> Breakdown, IReadOnlyList<(SheetItemData Item, decimal UnitCost, decimal ItemCost)> ResolvedItems)
        SheetDirectCost(SheetPricingInput input, ICalculationSource source)
    {
        var breakdown = new List<BreakdownItem>();
        var resolved = new List<(SheetItemData, decimal, decimal)>();
        decimal subtotal = 0;

        foreach (var item in input.Items)
        {
            decimal unitCost;
            if (item.Type == SheetItemType.Ingredient)
            {
                var ingredient = source.GetIngredient(item.IngredientId)
                    ?? throw new DomainException($"Ficha técnica: ingrediente '{item.Label}' não encontrado.");
                unitCost = UnitCostPerUseUnit(ingredient, item.Unit);
            }
            else
            {
                var preparation = source.GetPreparation(item.PreparationId)
                    ?? throw new DomainException($"Ficha técnica: preparo '{item.Label}' não encontrado.");
                var prepCost = PreparationCost(preparation, source, input.GlobalLossPercentage);
                unitCost = PreparationYieldUnitCost(preparation, prepCost.TotalCost, item.Unit);
            }

            var itemCost = Round4(unitCost * item.Quantity);
            subtotal += itemCost;
            breakdown.Add(new BreakdownItem(item.Label, itemCost));
            resolved.Add((item, unitCost, itemCost));
        }

        subtotal += input.PackagingCost;
        if (input.PackagingCost > 0)
            breakdown.Add(new BreakdownItem("Embalagem", Round4(input.PackagingCost)));

        var directCost = Round4(subtotal * (1 + (input.LossPercentage ?? 0) / 100));
        if (input.LossPercentage is > 0)
            breakdown.Add(new BreakdownItem($"Perda adicional ({input.LossPercentage}%)", Round4(directCost - subtotal)));

        return (directCost, breakdown, resolved);
    }

    // ---------------------------------------------------------------- §4 rateio de fixos

    /// <summary>
    /// Rateio de custos fixos (docs/06 §4):
    /// PerPizza → valor fixo por pizza; RevenuePercentage → percentual que reduz a margem disponível.
    /// </summary>
    public static FixedCostAllocation AllocateFixedCosts(IEnumerable<FixedCostInput> fixedCosts)
    {
        decimal perPizza = 0;
        decimal overheadPct = 0;

        foreach (var cost in fixedCosts)
        {
            switch (cost.AllocationType)
            {
                case AllocationType.PerPizza:
                    if (cost.ExpectedPizzasPerMonth is null or <= 0)
                        throw new DomainException(
                            "Custo fixo com rateio 'PorPizza' exige previsão de pizzas por mês maior que zero.");
                    perPizza += cost.MonthlyValue / cost.ExpectedPizzasPerMonth.Value;
                    break;

                case AllocationType.RevenuePercentage:
                    if (cost.ExpectedMonthlyRevenue is null or <= 0)
                        throw new DomainException(
                            "Custo fixo com rateio 'Percentual do faturamento' exige faturamento mensal esperado maior que zero.");
                    overheadPct += cost.MonthlyValue / cost.ExpectedMonthlyRevenue.Value * 100;
                    break;

                default:
                    throw new DomainException("Método de rateio desconhecido.");
            }
        }

        return new FixedCostAllocation(perPizza, overheadPct);
    }

    // ---------------------------------------------------------------- §5–§6 preço, lucro, margem

    /// <summary>
    /// Preço sugerido por margem sobre o preço de venda (docs/06 §5) e
    /// lucro/margem real/status (docs/06 §6).
    /// </summary>
    public static PricingResult PriceSheet(
        SheetPricingInput input,
        ICalculationSource source,
        IReadOnlyList<BreakdownItem>? externalBreakdown = null)
    {
        var (directCost, breakdown, _) = SheetDirectCost(input, source);
        return BuildPricingResult(
            directCost,
            input.Allocation.IndirectCostPerPizza,
            input.Allocation.FixedOverheadPercentage,
            input.MarginPercentageOverride ?? input.DefaultMarginPercentage,
            input.CardFeePercentage + input.DeliveryFeePercentage,
            externalBreakdown ?? breakdown);
    }

    /// <summary>Núcleo compartilhado §5–§6 a partir de custos já resolvidos.</summary>
    public static PricingResult BuildPricingResult(
        decimal directCost,
        decimal indirectCost,
        decimal fixedOverheadPercentage,
        decimal marginTarget,
        decimal totalFeesPercentage,
        IReadOnlyList<BreakdownItem> breakdown)
    {
        var totalCost = Round4(directCost + indirectCost);
        var available = 1 - marginTarget / 100 - totalFeesPercentage / 100 - fixedOverheadPercentage / 100;

        if (available <= 0)
            throw new DomainException("Margem + taxas atingem 100% — preço impossível.");

        // Preço de venda arredondado a 2 casas: é o valor que o cliente paga.
        var suggestedPrice = Round2(totalCost / available);
        var cardPrice = Round2(Math.Floor(suggestedPrice) + 0.90m);

        var taxUnrounded = suggestedPrice * totalFeesPercentage / 100;
        var taxValue = Round4(taxUnrounded);
        var profit = Round4(suggestedPrice - totalCost - taxUnrounded);
        var marginReal = suggestedPrice == 0
            ? 0
            : Round4(profit / suggestedPrice * 100);

        var status = marginReal >= marginTarget - MarginTolerance
            ? MarginStatus.Within
            : MarginStatus.NeedsAdjustment;

        var fullBreakdown = breakdown
            .Append(new BreakdownItem("Custo indireto (rateio)", Round4(indirectCost)))
            .ToList();

        return new PricingResult(
            directCost,
            Round4(indirectCost),
            totalCost,
            suggestedPrice,
            cardPrice,
            taxValue,
            profit,
            marginReal,
            marginTarget,
            status,
            fullBreakdown);
    }

    // ---------------------------------------------------------------- §8 mistas

    /// <summary>
    /// Pizza mista (docs/06 §8): custo médio entre os sabores, preço pelo maior.
    /// Funciona para 2 (meio a meio) ou 4 (quatro sabores).
    /// </summary>
    public static MistaResult Mista(
        IReadOnlyList<(string Name, decimal Cost, decimal Price)> flavors,
        decimal totalFeesPercentage,
        decimal marginTarget)
    {
        if (flavors.Count is not (2 or 4))
            throw new DomainException("Mista aceita exatamente 2 sabores (meio a meio) ou 4 (quatro sabores).");
        if (flavors.Any(f => f.Cost < 0 || f.Price <= 0))
            throw new DomainException("Todos os sabores da mista precisam de ficha técnica calculada para o tamanho escolhido.");

        var cost = Round4(flavors.Average(f => f.Cost));
        var price = Round2(flavors.Max(f => f.Price));
        var taxUnrounded = price * totalFeesPercentage / 100;
        var tax = Round4(taxUnrounded);
        var profit = Round4(price - cost - taxUnrounded);
        var margin = price == 0 ? 0 : Round4(profit / price * 100);
        var status = margin >= marginTarget - MarginTolerance
            ? MarginStatus.Within
            : MarginStatus.NeedsAdjustment;

        return new MistaResult(
            cost, price, tax, profit, margin, status,
            flavors.Select(f => new MistaFlavor(f.Name, Round4(f.Cost), Round2(f.Price))).ToList());
    }

    // ---------------------------------------------------------------- §9 combos

    /// <summary>Combo (docs/06 §9): custo/preço somados com desconto, ou preço fixo.</summary>
    public static ComboResult Combo(ComboPricingInput input)
    {
        if (input.Items.Count == 0)
            throw new DomainException("Combo precisa de pelo menos um item.");

        decimal cost = 0;
        decimal priceSum = 0;
        foreach (var item in input.Items)
        {
            if (item.Quantity <= 0)
                throw new DomainException("Quantidade dos itens do combo deve ser maior que zero.");
            cost += item.UnitCost * item.Quantity;
            priceSum += item.UnitPrice * item.Quantity;
        }

        cost -= input.DiscountValue;
        if (cost < 0)
            throw new DomainException("Desconto maior que o custo do combo.");

        var price = Round2(input.FixedPrice ?? priceSum - input.DiscountValue);
        if (price <= 0)
            throw new DomainException("Preço do combo deve ser maior que zero.");

        var fees = input.CardFeePercentage + input.DeliveryFeePercentage;
        var taxUnrounded = price * fees / 100;
        var tax = Round4(taxUnrounded);
        var profit = Round4(price - cost - taxUnrounded);
        var margin = Round4(profit / price * 100);
        var status = margin >= input.DefaultMarginPercentage - MarginTolerance
            ? MarginStatus.Within
            : MarginStatus.NeedsAdjustment;

        return new ComboResult(Round4(cost), price, tax, profit, margin, status);
    }

    // ---------------------------------------------------------------- §10 bebidas

    /// <summary>Bebida (docs/06 §10): multiplicador de markup sobre o custo de aquisição.</summary>
    public static (decimal SuggestedPrice, decimal MarginPercentage, MarginStatus Status)
        Beverage(decimal purchasePrice, decimal markupMultiplier, decimal marginTarget)
    {
        if (markupMultiplier <= 0)
            throw new DomainException("Multiplicador de markup deve ser maior que zero.");

        var price = Round2(purchasePrice * markupMultiplier);
        var margin = price == 0 ? 0 : Round4((price - purchasePrice) / price * 100);
        var status = margin >= marginTarget - MarginTolerance
            ? MarginStatus.Within
            : MarginStatus.NeedsAdjustment;

        return (price, margin, status);
    }

    /// <summary>Preço de cardápio arredondado para terminar em ,90 (docs/06 §7, informativo).</summary>
    public static decimal CardPrice(decimal suggestedPrice)
        => Round2(Math.Floor(suggestedPrice) + 0.90m);
}

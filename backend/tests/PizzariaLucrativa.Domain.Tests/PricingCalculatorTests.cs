using PizzariaLucrativa.Domain.Enums;
using PizzariaLucrativa.Domain.Exceptions;
using PizzariaLucrativa.Domain.Services;
using Xunit;

namespace PizzariaLucrativa.Domain.Tests;

/// <summary>
/// Golden files da engine de cálculo — docs/06 §12 (NF-T01: ≥ 15 casos).
/// Cada teste é a especificação executável das fórmulas.
/// </summary>
public class PricingCalculatorTests
{
    // -------------------------------------------------- fonte de teste em memória

    private sealed class MemorySource : ICalculationSource
    {
        private readonly Dictionary<Guid, IngredientData> _ingredients = new();
        private readonly Dictionary<Guid, PreparationData> _preparations = new();

        public MemorySource AddIngredient(IngredientData i) { _ingredients[i.Id] = i; return this; }
        public MemorySource AddPreparation(PreparationData p) { _preparations[p.Id] = p; return this; }

        public IngredientData? GetIngredient(Guid id) => _ingredients.GetValueOrDefault(id);
        public PreparationData? GetPreparation(Guid id) => _preparations.GetValueOrDefault(id);
    }

    private static readonly Guid FarinhaId = Guid.NewGuid();
    private static readonly Guid AguaId = Guid.NewGuid();
    private static readonly Guid FermentoId = Guid.NewGuid();
    private static readonly Guid MusarelaId = Guid.NewGuid();
    private static readonly Guid CalabresaId = Guid.NewGuid();
    private static readonly Guid MassaBrotoId = Guid.NewGuid();

    /// <summary>Cenário do exemplo numérico de referência (docs/05 §5).</summary>
    private static MemorySource ReferenceScenario()
    {
        var source = new MemorySource()
            .AddIngredient(new IngredientData(MusarelaId, "Muçarela", MeasureUnit.g, 5000, 32.90m, 3))
            .AddIngredient(new IngredientData(CalabresaId, "Calabresa", MeasureUnit.g, 1000, 9.50m, 0))
            .AddIngredient(new IngredientData(FarinhaId, "Farinha de trigo", MeasureUnit.g, 5000, 12.50m, 0))
            .AddIngredient(new IngredientData(AguaId, "Água", MeasureUnit.g, 1000, 0, 0))
            .AddIngredient(new IngredientData(FermentoId, "Fermento biológico", MeasureUnit.g, 100, 1.80m, 0));

        source.AddPreparation(new PreparationData(
            MassaBrotoId, "Massa broto", 10, MeasureUnit.un,
            new[]
            {
                new PreparationItemData(Guid.NewGuid(), SheetItemType.Ingredient, FarinhaId, null, 3000, MeasureUnit.g),
                new PreparationItemData(Guid.NewGuid(), SheetItemType.Ingredient, AguaId, null, 1900, MeasureUnit.g),
                new PreparationItemData(Guid.NewGuid(), SheetItemType.Ingredient, FermentoId, null, 200, MeasureUnit.g),
            }));

        return source;
    }

    private static SheetPricingInput ReferenceSheet(MemorySource source, decimal? loss = null, decimal? margin = null)
    {
        _ = source;
        return new SheetPricingInput(
            Items: new[]
            {
                new SheetItemData("Massa broto", SheetItemType.Preparation, Guid.Empty, MassaBrotoId, 1, MeasureUnit.un),
                new SheetItemData("Muçarela", SheetItemType.Ingredient, MusarelaId, Guid.Empty, 180, MeasureUnit.g),
                new SheetItemData("Calabresa", SheetItemType.Ingredient, CalabresaId, Guid.Empty, 100, MeasureUnit.g),
            },
            PackagingCost: 0.60m,
            LossPercentage: loss,
            MarginPercentageOverride: margin,
            DefaultMarginPercentage: 60,
            CardFeePercentage: 2.99m,
            DeliveryFeePercentage: 0,
            Allocation: new FixedCostAllocation(1.28m, 0),
            GlobalLossPercentage: 0);
    }

    // -------------------------------------------------- §1 — insumo

    [Fact] // golden 1
    public void Golden01_IngredientUnitCost_ComPerda3Percent()
    {
        // docs/06 §1: UnitCost = (PurchasePrice ÷ PurchaseQuantity) × (1 + Loss%/100)
        // 32,90 ÷ 5000 × 1,03 = 0,0067774 — a fórmula da especificação é a verdade.
        // OBS: docs/06 e docs/05 citam "0,006798", valor que corresponde a R$ 33,00
        // (33,00 ÷ 5000 × 1,03 = 0,006798 exato) — aparente erro de aritmética no
        // documento; ambos convergem para o mesmo resultado exibido (180 g → R$ 1,22).
        var ing = new IngredientData(Guid.NewGuid(), "Muçarela", MeasureUnit.g, 5000, 32.90m, 3);
        Assert.Equal(0.0067774m, PricingCalculator.IngredientUnitCost(ing), 7);

        // O valor citado na documentação é reproduzido com compra de R$ 33,00:
        var ing33 = new IngredientData(Guid.NewGuid(), "Muçarela", MeasureUnit.g, 5000, 33.00m, 3);
        Assert.Equal(0.006798m, PricingCalculator.IngredientUnitCost(ing33), 6);
    }

    [Fact] // golden 2
    public void Golden02_IngredientUnitCost_ConversaoKgParaG()
    {
        var ing = new IngredientData(Guid.NewGuid(), "Queijo kg", MeasureUnit.kg, 5, 45.00m, 0);
        // 45 ÷ 5 = 9,00/kg → custo por grama = 9,00 × 0,001 = 0,009
        // (golden "conversão kg→g ×0,001" refere-se ao CUSTO UNITÁRIO — docs/06 §1)
        Assert.Equal(0.009m, PricingCalculator.UnitCostPerUseUnit(ing, MeasureUnit.g), 6);
        // fatores de QUANTIDADE: 1 kg = 1000 g e 1 L = 1000 ml
        Assert.Equal(1000m, UnitConverter.Factor(MeasureUnit.kg, MeasureUnit.g));
        Assert.Equal(1000m, UnitConverter.Factor(MeasureUnit.L, MeasureUnit.ml));
        Assert.Equal(0.001m, UnitConverter.Factor(MeasureUnit.g, MeasureUnit.kg));
    }

    // -------------------------------------------------- §2 — preparos

    [Fact] // golden 3
    public void Golden03_PrepacaoSimples_CustoUnitario()
    {
        var source = ReferenceScenario();
        var result = PricingCalculator.PreparationCost(source.GetPreparation(MassaBrotoId)!, source);
        // 3000×0,0025 + 1900×0 + 200×0,018 = 7,50 + 0 + 3,60 = 11,10 → 1,11/un
        Assert.Equal(11.10m, result.TotalCost, 4);
        Assert.Equal(1.11m, result.UnitCost, 4);
    }

    [Fact] // golden 4
    public void Golden04_PrepacaoAninhado_DoisNiveis()
    {
        var source = ReferenceScenario();
        var recheioId = Guid.NewGuid();
        source.AddPreparation(new PreparationData(
            recheioId, "Recheio de frango", 10, MeasureUnit.un,
            new[]
            {
                new PreparationItemData(Guid.NewGuid(), SheetItemType.Preparation, null, MassaBrotoId, 2, MeasureUnit.un),
            }));

        var result = PricingCalculator.PreparationCost(source.GetPreparation(recheioId)!, source);
        // 2 × massa(1,11/un) = 2,22 → 0,222/un
        Assert.Equal(2.22m, result.TotalCost, 4);
        Assert.Equal(0.222m, result.UnitCost, 4);
    }

    [Fact] // golden 5
    public void Golden05_CicloEmPreparo_GeraExcecao()
    {
        var aId = Guid.NewGuid();
        var bId = Guid.NewGuid();
        var source = new MemorySource()
            .AddPreparation(new PreparationData(aId, "A", 1, MeasureUnit.un, new[]
            {
                new PreparationItemData(Guid.NewGuid(), SheetItemType.Preparation, null, bId, 1, MeasureUnit.un)
            }))
            .AddPreparation(new PreparationData(bId, "B", 1, MeasureUnit.un, new[]
            {
                new PreparationItemData(Guid.NewGuid(), SheetItemType.Preparation, null, aId, 1, MeasureUnit.un)
            }));

        Assert.Throws<CycleDetectedException>(
            () => PricingCalculator.PreparationCost(source.GetPreparation(aId)!, source));
    }

    [Fact]
    public void Preparo_AutoReferencia_GeraCiclo()
    {
        var aId = Guid.NewGuid();
        var source = new MemorySource()
            .AddPreparation(new PreparationData(aId, "Auto", 1, MeasureUnit.un, new[]
            {
                new PreparationItemData(Guid.NewGuid(), SheetItemType.Preparation, null, aId, 1, MeasureUnit.un)
            }));

        Assert.Throws<CycleDetectedException>(
            () => PricingCalculator.PreparationCost(source.GetPreparation(aId)!, source));
    }

    // -------------------------------------------------- §3 — custo direto

    [Fact] // golden 6
    public void Golden06_FichaComPerdaAdicional_SubtotalVezes102()
    {
        var source = ReferenceScenario();
        var (subtotal, _, _) = PricingCalculator.SheetDirectCost(ReferenceSheet(source), source);
        var (directCost, _, _) = PricingCalculator.SheetDirectCost(ReferenceSheet(source, loss: 2), source);
        // docs/06 §3: perda incide sobre o subtotal (itens + embalagem) — × 1,02
        var expected = decimal.Round(subtotal * 1.02m, 4, MidpointRounding.AwayFromZero);
        Assert.Equal(expected, directCost, 4);
    }

    [Fact] // golden 6 (continuação — custo direto do exemplo de referência)
    public void Golden06b_FichaCalabresaBroto_DiretoIgual388()
    {
        var source = ReferenceScenario();
        var (directCost, _, _) = PricingCalculator.SheetDirectCost(ReferenceSheet(source), source);
        // docs/05 §5: DirectCost = 3,88
        Assert.Equal(3.88m, directCost, 2);
    }

    // -------------------------------------------------- §4 — rateio

    [Fact] // golden 7
    public void Golden07_RateioPerPizza_Igual128()
    {
        var allocation = PricingCalculator.AllocateFixedCosts(new[]
        {
            new FixedCostInput(3200, AllocationType.PerPizza, 2500, null)
        });
        // 3.200 ÷ 2.500 = 1,28
        Assert.Equal(1.28m, allocation.IndirectCostPerPizza, 4);
        Assert.Equal(0, allocation.FixedOverheadPercentage);
    }

    [Fact] // golden 8
    public void Golden08_RateioRevenuePercentage_PorcentualCorreto()
    {
        var allocation = PricingCalculator.AllocateFixedCosts(new[]
        {
            new FixedCostInput(1500, AllocationType.RevenuePercentage, null, 30000)
        });
        // 1.500 ÷ 30.000 × 100 = 5%
        Assert.Equal(5m, allocation.FixedOverheadPercentage, 4);
        Assert.Equal(0, allocation.IndirectCostPerPizza);
    }

    [Fact]
    public void Rateio_CustosMistos_SomaAmbos()
    {
        var allocation = PricingCalculator.AllocateFixedCosts(new[]
        {
            new FixedCostInput(1000, AllocationType.PerPizza, 2000, null),
            new FixedCostInput(500, AllocationType.RevenuePercentage, null, 25000),
        });
        Assert.Equal(0.5m, allocation.IndirectCostPerPizza, 4);
        Assert.Equal(2m, allocation.FixedOverheadPercentage, 4);
    }

    // -------------------------------------------------- §5 — preço

    [Fact] // golden 9
    public void Golden09_PrecoPorMargem60_Igual1394()
    {
        var source = ReferenceScenario();
        var result = PricingCalculator.PriceSheet(ReferenceSheet(source), source);
        // (3,88 + 1,28) ÷ (1 − 0,60 − 0,0299) = 5,16 ÷ 0,3701 = 13,94
        Assert.Equal(13.94m, result.SuggestedPrice, 2);
        Assert.Equal(1.28m, result.IndirectCost, 4);
    }

    [Fact] // golden 10
    public void Golden10_MargemMaisTaxasIgual100_PrecoImpossivel()
    {
        var source = ReferenceScenario();
        var input = ReferenceSheet(source, margin: 100);
        var ex = Assert.Throws<DomainException>(() => PricingCalculator.PriceSheet(input, source));
        Assert.Contains("preço impossível", ex.Message);
    }

    // -------------------------------------------------- §6 — lucro, margem, status

    [Fact] // golden 11
    public void Golden11_StatusWithin_ComTolerancia()
    {
        var source = ReferenceScenario();
        var result = PricingCalculator.PriceSheet(ReferenceSheet(source), source);
        // docs/05 §5: Lucro 8,36 · Margem 60,0% → Within
        Assert.Equal(8.36m, result.EstimatedProfit, 2);
        Assert.True(result.MarginRealPercentage >= 60 - PricingCalculator.MarginTolerance);
        Assert.Equal(MarginStatus.Within, result.Status);
    }

    [Fact] // golden 12
    public void Golden12_StatusNeedsAdjustment_QuandoMargemRealBaixa()
    {
        // Preço bruto 12,50475 arredonda para 12,50; o resíduo empurra a margem
        // real (59,98%) abaixo de 60 − 0,01 de tolerância → NeedsAdjustment.
        var result = PricingCalculator.BuildPricingResult(
            directCost: 5.0019m,
            indirectCost: 0,
            fixedOverheadPercentage: 0,
            marginTarget: 60,
            totalFeesPercentage: 0,
            breakdown: Array.Empty<BreakdownItem>());

        Assert.Equal(12.50m, result.SuggestedPrice);
        Assert.Equal(MarginStatus.NeedsAdjustment, result.Status);
        Assert.True(result.MarginRealPercentage < 60);
    }

    [Fact]
    public void BuildPricingResult_PrecoQueNaoArredondaParaBaixo_DentroDaMargem()
    {
        // Referência: arredondamento para cima mantém a margem no alvo.
        var result = PricingCalculator.BuildPricingResult(
            directCost: 3.88m, indirectCost: 1.28m, fixedOverheadPercentage: 0,
            marginTarget: 60, totalFeesPercentage: 2.99m,
            breakdown: Array.Empty<BreakdownItem>());
        Assert.Equal(MarginStatus.Within, result.Status);
    }

    // -------------------------------------------------- §8 — mistas

    [Fact] // golden 13
    public void Golden13_MeioAMeio_CustoMedio()
    {
        var result = PricingCalculator.Mista(
            new[] { ("Calabresa", 5.16m, 13.94m), ("Frango", 6.00m, 15.90m) },
            totalFeesPercentage: 2.99m,
            marginTarget: 60);
        Assert.Equal(5.58m, result.Cost, 4); // (5,16 + 6,00) ÷ 2
    }

    [Fact] // golden 14
    public void Golden14_MeioAMeio_PrecoPeloMaior()
    {
        var result = PricingCalculator.Mista(
            new[] { ("Calabresa", 5.16m, 13.94m), ("Frango", 6.00m, 15.90m) },
            totalFeesPercentage: 2.99m,
            marginTarget: 60);
        Assert.Equal(15.90m, result.Price, 4); // max(13,94; 15,90)
        Assert.Equal("Calabresa", result.Flavors[0].Name);
        Assert.Equal("Frango", result.Flavors[1].Name);
    }

    [Fact]
    public void QuatroSabores_CustoIgualmenteDividido()
    {
        var result = PricingCalculator.Mista(
            new[] { ("A", 4.00m, 13.94m), ("B", 6.00m, 13.94m), ("C", 8.00m, 13.94m), ("D", 10.00m, 13.94m) },
            totalFeesPercentage: 2.99m,
            marginTarget: 60);
        Assert.Equal(7.00m, result.Cost, 4); // (4+6+8+10)/4
        Assert.Equal(4, result.Flavors.Count);
    }

    [Fact]
    public void Mista_QuantidadeInvalida_Erro()
    {
        Assert.Throws<DomainException>(() => PricingCalculator.Mista(
            new[] { ("A", 1m, 10m) }, 0, 60));
    }

    // -------------------------------------------------- §9 — combos

    [Fact] // golden 15
    public void Golden15_ComboComDesconto_LucroCorreto()
    {
        var result = PricingCalculator.Combo(new ComboPricingInput(
            Items: new[]
            {
                new ComboItemInput(Guid.NewGuid(), Guid.NewGuid(), 1, UnitCost: 5.16m, UnitPrice: 13.94m),
                new ComboItemInput(Guid.NewGuid(), null, 2, UnitCost: 3.00m, UnitPrice: 6.50m),
            },
            DiscountValue: 5,
            FixedPrice: null,
            CardFeePercentage: 2.99m,
            DeliveryFeePercentage: 0,
            DefaultMarginPercentage: 60));

        // custo: 5,16 + 6,00 − 5,00 = 6,16
        Assert.Equal(6.16m, result.TotalCost, 4);
        // preço: 13,94 + 13,00 − 5,00 = 21,94
        Assert.Equal(21.94m, result.TotalPrice, 4);
        var tax = 21.94m * 0.0299m;
        Assert.Equal(decimal.Round(21.94m - 6.16m - tax, 4), result.Profit, 4);
    }

    [Fact] // golden 16
    public void Golden16_ComboComPrecoFixo_UsaFixedPrice()
    {
        var result = PricingCalculator.Combo(new ComboPricingInput(
            Items: new[]
            {
                new ComboItemInput(Guid.NewGuid(), Guid.NewGuid(), 1, UnitCost: 5.16m, UnitPrice: 13.94m),
            },
            DiscountValue: 0,
            FixedPrice: 19.90m,
            CardFeePercentage: 0,
            DeliveryFeePercentage: 0,
            DefaultMarginPercentage: 60));

        Assert.Equal(19.90m, result.TotalPrice, 4);
        Assert.Equal(19.90m - 5.16m, result.Profit, 4);
    }

    [Fact]
    public void Combo_DescontoMaiorQueCusto_Erro()
    {
        Assert.Throws<DomainException>(() => PricingCalculator.Combo(new ComboPricingInput(
            Items: new[] { new ComboItemInput(Guid.NewGuid(), Guid.NewGuid(), 1, 5m, 13.94m) },
            DiscountValue: 10,
            FixedPrice: null,
            CardFeePercentage: 0,
            DeliveryFeePercentage: 0,
            DefaultMarginPercentage: 60)));
    }

    // -------------------------------------------------- §10 — bebidas

    [Fact]
    public void Bebida_MarkupPadrao_DobraPreco()
    {
        var (price, margin, status) = PricingCalculator.Beverage(5.00m, 2.0m, 60);
        Assert.Equal(10.00m, price, 4);
        Assert.Equal(50m, margin, 4);
        Assert.Equal(MarginStatus.NeedsAdjustment, status); // 50% < 60%
    }

    // -------------------------------------------------- §11 — recálculo em cadeia (caso 17 via motor)

    [Fact] // golden 17 (componente de cálculo — a orquestração é testada na API)
    public void Golden17_RecalculoEmCadeia_UmEventoNItensAtualizados()
    {
        // evento: preço da muçarela sobe 20% → custo da ficha e status respondem em cadeia
        var source = ReferenceScenario();
        var sheetInput = ReferenceSheet(source);
        var before = PricingCalculator.PriceSheet(sheetInput, source);
        Assert.Equal(MarginStatus.Within, before.Status);

        var old = source.GetIngredient(MusarelaId)!;
        var newIngredient = old with { PurchasePrice = old.PurchasePrice * 1.2m };
        var bumped = new MemorySource()
            .AddIngredient(newIngredient)
            .AddIngredient(source.GetIngredient(CalabresaId)!)
            .AddIngredient(source.GetIngredient(FarinhaId)!)
            .AddIngredient(source.GetIngredient(AguaId)!)
            .AddIngredient(source.GetIngredient(FermentoId)!)
            .AddPreparation(source.GetPreparation(MassaBrotoId)!);

        var after = PricingCalculator.PriceSheet(sheetInput, bumped);
        Assert.True(after.DirectCost > before.DirectCost, "custo direto deve subir");
        Assert.True(after.SuggestedPrice > before.SuggestedPrice, "preço sugerido deve subir");

        // mesma entrada duas vezes → mesmo resultado (idempotência, NF-R02)
        var again = PricingCalculator.PriceSheet(sheetInput, bumped);
        Assert.Equal(after.DirectCost, again.DirectCost);
        Assert.Equal(after.TotalCost, again.TotalCost);
        Assert.Equal(after.SuggestedPrice, again.SuggestedPrice);
        Assert.Equal(after.EstimatedProfit, again.EstimatedProfit);
        Assert.Equal(after.MarginRealPercentage, again.MarginRealPercentage);
        Assert.Equal(after.Status, again.Status);
    }

    // -------------------------------------------------- extras de proteção

    [Fact]
    public void ProfundidadeMaxima_GeraErroClaro()
    {
        var source = new MemorySource();
        var ids = Enumerable.Range(0, 8).Select(_ => Guid.NewGuid()).ToArray();
        for (var i = 0; i < ids.Length - 1; i++)
        {
            var next = ids[i + 1];
            source.AddPreparation(new PreparationData(ids[i], $"P{i}", 1, MeasureUnit.un, new[]
            {
                new PreparationItemData(Guid.NewGuid(), SheetItemType.Preparation, null, next, 1, MeasureUnit.un)
            }));
        }
        source.AddPreparation(new PreparationData(ids[^1], "Pfim", 1, MeasureUnit.un, Array.Empty<PreparationItemData>()));

        var ex = Assert.Throws<DomainException>(() =>
            PricingCalculator.PreparationCost(source.GetPreparation(ids[0])!, source));
        Assert.Contains("níveis", ex.Message);
    }

    [Fact]
    public void CardPrice_TerminaEm90()
    {
        Assert.Equal(13.90m, PricingCalculator.CardPrice(13.94m));
        Assert.Equal(14.90m, PricingCalculator.CardPrice(14.05m));
        Assert.Equal(14.90m, PricingCalculator.CardPrice(14.90m));
    }

    [Fact]
    public void UnidadesIncompativeis_GeraErroClaro()
    {
        Assert.Throws<DomainException>(() => UnitConverter.Convert(1, MeasureUnit.g, MeasureUnit.un));
    }
}

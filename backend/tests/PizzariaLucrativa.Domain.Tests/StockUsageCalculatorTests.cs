using PizzariaLucrativa.Domain.Enums;
using PizzariaLucrativa.Domain.Exceptions;
using PizzariaLucrativa.Domain.Services;
using Xunit;

namespace PizzariaLucrativa.Domain.Tests;

/// <summary>
/// Testes de domínio da expansão de consumo de estoque (docs/10, Sprint 2).
/// Cobre conversão de unidade, preparo aninhado, mistas (50/50 e 25% cada) e ciclos.
/// </summary>
public class StockUsageCalculatorTests
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

    private static SheetItemData IngredientItem(Guid id, decimal quantity, MeasureUnit unit = MeasureUnit.g)
        => new("item", SheetItemType.Ingredient, id, Guid.Empty, quantity, unit);

    private static SheetItemData PreparationItem(Guid id, decimal quantity, MeasureUnit unit = MeasureUnit.un)
        => new("item", SheetItemType.Preparation, Guid.Empty, id, quantity, unit);

    // -------------------------------------------------- conversão de unidade

    [Fact]
    public void Insumo_CompradoEmKg_UsadoEmGramas_ConverteParaUnidadeDeCompra()
    {
        var muçarela = Guid.NewGuid();
        var source = new MemorySource()
            .AddIngredient(new IngredientData(muçarela, "Muçarela", MeasureUnit.kg, 5, 45m, 0));

        var totals = StockUsageCalculator.AccumulateSheet(
            new[] { IngredientItem(muçarela, 180) }, source);

        // 180 g = 0,18 kg (a compra é em kg → o consumo é registrado em kg).
        Assert.Equal(0.18m, totals[muçarela], 4);
    }

    [Fact]
    public void Insumo_CompradoEmLitro_UsadoEmMl_Converte()
    {
        var leite = Guid.NewGuid();
        var source = new MemorySource()
            .AddIngredient(new IngredientData(leite, "Leite", MeasureUnit.L, 1, 5m, 0));

        var totals = StockUsageCalculator.AccumulateSheet(
            new[] { IngredientItem(leite, 250, MeasureUnit.ml) }, source);

        Assert.Equal(0.25m, totals[leite], 4);
    }

    [Fact]
    public void Insumo_UnidadeIncompativel_GeraErro()
    {
        var queijo = Guid.NewGuid();
        var source = new MemorySource()
            .AddIngredient(new IngredientData(queijo, "Queijo", MeasureUnit.g, 1000, 30m, 0));

        Assert.Throws<DomainException>(() => StockUsageCalculator.AccumulateSheet(
            new[] { IngredientItem(queijo, 1, MeasureUnit.un) }, source));
    }

    // -------------------------------------------------- preparo aninhado

    [Fact]
    public void PreparoAninhado_MultiplicaPeloRendimentoEmCadaNivel()
    {
        var farinha = Guid.NewGuid();
        var massaId = Guid.NewGuid();
        var recheioId = Guid.NewGuid();

        var source = new MemorySource()
            .AddIngredient(new IngredientData(farinha, "Farinha", MeasureUnit.g, 5000, 12.50m, 0))
            .AddPreparation(new PreparationData(massaId, "Massa", 10, MeasureUnit.un, new[]
            {
                new PreparationItemData(Guid.NewGuid(), SheetItemType.Ingredient, farinha, null, 3000, MeasureUnit.g)
            }))
            .AddPreparation(new PreparationData(recheioId, "Recheio", 10, MeasureUnit.un, new[]
            {
                new PreparationItemData(Guid.NewGuid(), SheetItemType.Preparation, null, massaId, 2, MeasureUnit.un)
            }));

        var totals = StockUsageCalculator.AccumulateSheet(
            new[] { PreparationItem(recheioId, 1) }, source);

        // 1 un de recheio → 2/10 = 0,2 un de massa → 0,2/10 × 3000 g = 60 g de farinha.
        Assert.Equal(60m, totals[farinha], 4);
    }

    [Fact]
    public void PreparoAninhado_RendimentoZero_GeraErro()
    {
        var farinha = Guid.NewGuid();
        var massaId = Guid.NewGuid();
        var source = new MemorySource()
            .AddIngredient(new IngredientData(farinha, "Farinha", MeasureUnit.g, 5000, 12.50m, 0))
            .AddPreparation(new PreparationData(massaId, "Massa", 0, MeasureUnit.un, new[]
            {
                new PreparationItemData(Guid.NewGuid(), SheetItemType.Ingredient, farinha, null, 3000, MeasureUnit.g)
            }));

        Assert.Throws<DomainException>(() => StockUsageCalculator.AccumulateSheet(
            new[] { PreparationItem(massaId, 1) }, source));
    }

    // -------------------------------------------------- mistas

    [Fact]
    public void Mista5050_CadaSaborEntraComMetadeDaFicha()
    {
        var muçarela = Guid.NewGuid();
        var calabresa = Guid.NewGuid();
        var source = new MemorySource()
            .AddIngredient(new IngredientData(muçarela, "Muçarela", MeasureUnit.g, 5000, 32.90m, 0))
            .AddIngredient(new IngredientData(calabresa, "Calabresa", MeasureUnit.g, 1000, 9.50m, 0));

        var totals = new Dictionary<Guid, decimal>();
        StockUsageCalculator.AccumulateSheet(
            new[] { IngredientItem(muçarela, 180) }, source, totals, scale: 0.5m);
        StockUsageCalculator.AccumulateSheet(
            new[] { IngredientItem(calabresa, 100) }, source, totals, scale: 0.5m);

        Assert.Equal(90m, totals[muçarela], 4);
        Assert.Equal(50m, totals[calabresa], 4);
    }

    [Fact]
    public void QuatroSabores_CadaFracaoEntraCom25PorCento()
    {
        var muçarela = Guid.NewGuid();
        var source = new MemorySource()
            .AddIngredient(new IngredientData(muçarela, "Muçarela", MeasureUnit.g, 5000, 32.90m, 0));

        var totals = new Dictionary<Guid, decimal>();
        for (var i = 0; i < 4; i++)
            StockUsageCalculator.AccumulateSheet(
                new[] { IngredientItem(muçarela, 100) }, source, totals, scale: 0.25m);

        // 4 × (100 g × 0,25) = 100 g.
        Assert.Equal(100m, totals[muçarela], 4);
    }

    [Fact]
    public void Mista_MesmoInsumoEmDoisSabores_SomaAsFracoes()
    {
        var muçarela = Guid.NewGuid();
        var source = new MemorySource()
            .AddIngredient(new IngredientData(muçarela, "Muçarela", MeasureUnit.g, 5000, 32.90m, 0));

        var totals = new Dictionary<Guid, decimal>();
        StockUsageCalculator.AccumulateSheet(
            new[] { IngredientItem(muçarela, 180) }, source, totals, scale: 0.5m);
        StockUsageCalculator.AccumulateSheet(
            new[] { IngredientItem(muçarela, 100) }, source, totals, scale: 0.5m);

        // (180 + 100) × 0,5 = 140 g.
        Assert.Equal(140m, totals[muçarela], 4);
    }

    // -------------------------------------------------- ciclos e profundidade

    [Fact]
    public void CicloEmPreparo_Estoura()
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

        Assert.Throws<CycleDetectedException>(() => StockUsageCalculator.AccumulateSheet(
            new[] { PreparationItem(aId, 1) }, source));
    }

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

        var ex = Assert.Throws<DomainException>(() => StockUsageCalculator.AccumulateSheet(
            new[] { PreparationItem(ids[0], 1) }, source));
        Assert.Contains("níveis", ex.Message);
    }
}

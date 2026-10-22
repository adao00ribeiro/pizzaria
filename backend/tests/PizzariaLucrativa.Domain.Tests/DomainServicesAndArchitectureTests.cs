using PizzariaLucrativa.Domain.Entities;
using PizzariaLucrativa.Domain.Enums;
using PizzariaLucrativa.Domain.Exceptions;
using PizzariaLucrativa.Domain.Services;

namespace PizzariaLucrativa.Domain.Tests;

/// <summary>
/// Testes unitários para utilitários de domínio (<see cref="SlugGenerator"/>,
/// <see cref="OrderCodeGenerator"/>, <see cref="UnitConverter"/>) e auditoria automatizada
/// da Regra de Dependência da Clean Architecture.
/// </summary>
public sealed class DomainServicesAndArchitectureTests
{
    [Theory]
    [InlineData("Pizzaria do Zé!", "pizzaria-do-ze")]
    [InlineData("Forno & Lenha - São Paulo", "forno-lenha-sao-paulo")]
    [InlineData("  PIZZARIA   ÁGUIA   DOURADA  ", "pizzaria-aguia-dourada")]
    [InlineData("Dom Corleone 1999", "dom-corleone-1999")]
    [InlineData("###", "")]
    [InlineData("ab", "")]
    [InlineData("", "")]
    public void SlugGenerator_Generate_NormalizesAccentsSpecialCharsSpacesAndEnforcesMinLength(
        string rawName,
        string expectedSlug)
    {
        var actual = SlugGenerator.Generate(rawName);

        Assert.Equal(expectedSlug, actual);
    }

    [Fact]
    public void SlugGenerator_Generate_TruncatesToMaxLengthWithoutTrailingDash()
    {
        var longName = "Pizzaria Artesanal Tradicional Napolitana da Família Real";

        var slug = SlugGenerator.Generate(longName);

        Assert.True(slug.Length <= SlugGenerator.MaxLength);
        Assert.False(slug.EndsWith('-'));
        Assert.True(SlugGenerator.IsValid(slug));
    }

    [Fact]
    public void SlugGenerator_WithSuffix_AppendsNumericSuffixAndRespectsMaxLength()
    {
        var normal = SlugGenerator.WithSuffix("pizzaria-do-ze", 2);
        Assert.Equal("pizzaria-do-ze-2", normal);
        Assert.True(SlugGenerator.IsValid(normal));

        var maxBase = new string('a', SlugGenerator.MaxLength);
        var suffixedFromMax = SlugGenerator.WithSuffix(maxBase, 12);

        Assert.Equal(SlugGenerator.MaxLength, suffixedFromMax.Length);
        Assert.EndsWith("-12", suffixedFromMax, StringComparison.Ordinal);
        Assert.True(SlugGenerator.IsValid(suffixedFromMax));
    }

    [Theory]
    [InlineData("pizzaria-do-ze", true)]
    [InlineData("abc", true)]
    [InlineData("-pizzaria", false)]
    [InlineData("pizzaria-", false)]
    [InlineData("Pizzaria", false)]
    [InlineData("ab", false)]
    public void SlugGenerator_IsValid_ValidatesFormatAndBounds(string candidate, bool expectedValid)
    {
        Assert.Equal(expectedValid, SlugGenerator.IsValid(candidate));
    }

    [Fact]
    public void OrderCodeGenerator_Generate_ProducesValidSixCharCodeWithoutAmbiguousCharacters_AndIsUnique()
    {
        var generatedCodes = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < 64; i++)
        {
            var code = OrderCodeGenerator.Generate();

            Assert.Equal(OrderCodeGenerator.Length, code.Length);
            Assert.True(OrderCodeGenerator.IsValid(code));
            Assert.DoesNotContain('O', code);
            Assert.DoesNotContain('0', code);
            Assert.DoesNotContain('I', code);
            Assert.DoesNotContain('1', code);
            generatedCodes.Add(code);
        }

        // Com 32^6 (>1 bilhão) combinações possíveis, 64 amostras criptográficas devem produzir alta unicidade.
        Assert.True(generatedCodes.Count > 55);
    }

    [Theory]
    [InlineData("ABC234", true)]
    [InlineData("ABCDE", false)]
    [InlineData("ABCDEFG", false)]
    [InlineData("ABCO23", false)]
    [InlineData("ABC023", false)]
    [InlineData("ABCI23", false)]
    [InlineData("ABC123", false)]
    [InlineData("abc234", false)]
    public void OrderCodeGenerator_IsValid_RejectsAmbiguousOrMalformedCodes(string code, bool expectedValid)
    {
        Assert.Equal(expectedValid, OrderCodeGenerator.IsValid(code));
    }

    [Theory]
    [InlineData(1.5, MeasureUnit.kg, MeasureUnit.g, 1500)]
    [InlineData(500, MeasureUnit.g, MeasureUnit.kg, 0.5)]
    [InlineData(2.0, MeasureUnit.L, MeasureUnit.ml, 2000)]
    [InlineData(250, MeasureUnit.ml, MeasureUnit.L, 0.25)]
    [InlineData(350, MeasureUnit.g, MeasureUnit.g, 350)]
    [InlineData(3, MeasureUnit.kg, MeasureUnit.kg, 3)]
    [InlineData(100, MeasureUnit.ml, MeasureUnit.ml, 100)]
    [InlineData(1, MeasureUnit.L, MeasureUnit.L, 1)]
    [InlineData(10, MeasureUnit.un, MeasureUnit.un, 10)]
    [InlineData(4, MeasureUnit.porcao, MeasureUnit.porcao, 4)]
    public void UnitConverter_Convert_ValidAndSameUnitConversions_ReturnsExpectedValue(
        decimal value,
        MeasureUnit from,
        MeasureUnit to,
        decimal expected)
    {
        var converted = UnitConverter.Convert(value, from, to);

        Assert.Equal(expected, converted);
    }

    [Theory]
    [InlineData(MeasureUnit.kg, MeasureUnit.ml)]
    [InlineData(MeasureUnit.un, MeasureUnit.g)]
    [InlineData(MeasureUnit.L, MeasureUnit.kg)]
    [InlineData(MeasureUnit.porcao, MeasureUnit.g)]
    [InlineData(MeasureUnit.g, MeasureUnit.un)]
    public void UnitConverter_Convert_IncompatibleUnits_ThrowsDomainException(
        MeasureUnit from,
        MeasureUnit to)
    {
        Assert.Throws<DomainException>(() => UnitConverter.Convert(1m, from, to));
    }

    [Fact]
    public void CleanArchitecture_DomainAssembly_MustNotReferenceOuterLayersOrFrameworks()
    {
        var domainAssembly = typeof(Tenant).Assembly;
        var referencedNames = domainAssembly
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToArray();

        var forbiddenPrefixes = new[]
        {
            "PizzariaLucrativa.Application",
            "PizzariaLucrativa.Infrastructure",
            "PizzariaLucrativa.Api",
            "Microsoft.EntityFrameworkCore",
            "Microsoft.AspNetCore"
        };

        foreach (var forbidden in forbiddenPrefixes)
        {
            Assert.DoesNotContain(
                referencedNames,
                name => name.StartsWith(forbidden, StringComparison.OrdinalIgnoreCase));
        }
    }
}

using PizzariaLucrativa.Domain.Enums;
using PizzariaLucrativa.Domain.Exceptions;

namespace PizzariaLucrativa.Domain.Services;

/// <summary>
/// Conversão de unidades na fronteira (docs/06 §1): 1 kg = 1000 g, 1 L = 1000 ml.
/// Unidades incompatíveis (ex.: g × un) geram erro claro.
/// </summary>
public static class UnitConverter
{
    /// <summary>Fator para converter 1 unidade <paramref name="from"/> em <paramref name="to"/>.</summary>
    public static decimal Factor(MeasureUnit from, MeasureUnit to)
    {
        if (from == to)
            return 1m;

        return (from, to) switch
        {
            (MeasureUnit.kg, MeasureUnit.g) => 1000m,
            (MeasureUnit.g, MeasureUnit.kg) => 0.001m,
            (MeasureUnit.L, MeasureUnit.ml) => 1000m,
            (MeasureUnit.ml, MeasureUnit.L) => 0.001m,
            _ => throw new DomainException(
                $"Não é possível converter a unidade '{from}' para '{to}'.")
        };
    }

    public static decimal Convert(decimal value, MeasureUnit from, MeasureUnit to)
        => value * Factor(from, to);
}

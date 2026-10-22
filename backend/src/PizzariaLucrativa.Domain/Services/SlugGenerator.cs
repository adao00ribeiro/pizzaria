using System.Text;
using System.Text.RegularExpressions;

namespace PizzariaLucrativa.Domain.Services;

/// <summary>
/// Geração/normalização do slug público da pizzaria (docs/10, Sprint 1).
/// Regras: minúsculas, sem acentos, apenas a-z/0-9/hífen, 3..40 caracteres,
/// sem hífen nas pontas.
/// </summary>
public static partial class SlugGenerator
{
    public const int MaxLength = 40;
    public const int MinLength = 3;

    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{1,38}[a-z0-9])$", RegexOptions.CultureInvariant)]
    private static partial Regex ValidPattern();

    /// <summary>Gera um slug a partir do nome (ex.: "Pizzaria do Zé!" → "pizzaria-do-ze").</summary>
    public static string Generate(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var normalized = name.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        var lastWasDash = false;

        foreach (var character in normalized)
        {
            var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == System.Globalization.UnicodeCategory.NonSpacingMark)
                continue;

            var lower = char.ToLowerInvariant(character);
            if (lower is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                builder.Append(lower);
                lastWasDash = false;
            }
            else if (!lastWasDash && builder.Length > 0)
            {
                builder.Append('-');
                lastWasDash = true;
            }
        }

        var slug = builder.ToString().Trim('-');
        if (slug.Length > MaxLength)
            slug = slug[..MaxLength].Trim('-');

        // Nome sem caracteres aproveitáveis (ex.: "###"): evita slug vazio.
        return slug.Length < MinLength ? string.Empty : slug;
    }

    /// <summary>Valida o formato do slug informado pelo owner.</summary>
    public static bool IsValid(string? slug)
        => !string.IsNullOrWhiteSpace(slug)
           && slug.Length is >= MinLength and <= MaxLength
           && ValidPattern().IsMatch(slug);

    /// <summary>
    /// Aplica o sufixo numérico usado para resolver colisão (-2, -3...), respeitando o limite.
    /// </summary>
    public static string WithSuffix(string baseSlug, int suffix)
    {
        var suffixText = $"-{suffix}";
        var maxBase = MaxLength - suffixText.Length;
        var trimmed = baseSlug.Length > maxBase ? baseSlug[..maxBase].Trim('-') : baseSlug;
        return trimmed + suffixText;
    }
}

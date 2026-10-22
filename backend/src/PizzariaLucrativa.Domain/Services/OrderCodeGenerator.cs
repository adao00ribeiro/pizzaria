using System.Security.Cryptography;

namespace PizzariaLucrativa.Domain.Services;

/// <summary>
/// Geração do código de acompanhamento do pedido (docs/10, premissa P2).
/// Seis caracteres de um alfabeto sem letras/dígitos ambíguos (O, 0, I, 1) para
/// o cliente conseguir digitar no site sem confundir caracteres.
/// </summary>
public static class OrderCodeGenerator
{
    /// <summary>32 caracteres (A–Z e 2–9 sem O, I, 0, 1).</summary>
    public const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public const int Length = 6;

    /// <summary>Gera um código aleatório criptograficamente seguro.</summary>
    public static string Generate()
    {
        var code = new char[Length];
        for (var i = 0; i < Length; i++)
            code[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return new string(code);
    }

    /// <summary>Valida formato e alfabeto do código informado (busca pública).</summary>
    public static bool IsValid(string? code)
        => !string.IsNullOrWhiteSpace(code)
           && code.Length == Length
           && code.All(c => Alphabet.Contains(c, StringComparison.Ordinal));
}

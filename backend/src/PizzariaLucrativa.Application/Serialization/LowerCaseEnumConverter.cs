using System.Text.Json;
using System.Text.Json.Serialization;

namespace PizzariaLucrativa.Application.Serialization;

/// <summary>
/// Serializa enums de contrato como string em camelCase (ex.: <c>bar</c>, <c>currency</c>),
/// independentemente da política global da API. Usado pelos DTOs de IA para manter o contrato
/// de <c>ChartSpec</c> e dos blocos de resposta estável para o frontend e o Gemini.
/// </summary>
public sealed class LowerCaseEnumConverter<TEnum> : JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    public override TEnum Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (string.IsNullOrWhiteSpace(value) || !Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed))
            throw new JsonException($"Valor inválido para {typeof(TEnum).Name}.");

        return parsed;
    }

    public override void Write(
        Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
        => writer.WriteStringValue(JsonNamingPolicy.CamelCase.ConvertName(value.ToString()));
}

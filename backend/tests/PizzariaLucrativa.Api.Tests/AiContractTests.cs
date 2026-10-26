using System.Text.Json;
using System.Text.Json.Serialization;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Serialization;
using PizzariaLucrativa.Domain.Enums;

namespace PizzariaLucrativa.Api.Tests;

/// <summary>
/// Congela os contratos públicos da IA definidos na IA-1: enums do ChartSpec em minúsculas,
/// camelCase e bloco de resposta com os campos exigidos pelo frontend.
/// </summary>
public sealed class AiContractTests
{
    private static readonly JsonSerializerOptions ApiJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters =
        {
            new LowerCaseEnumConverter<AnswerBlockType>(),
            new LowerCaseEnumConverter<ChartKind>(),
            new LowerCaseEnumConverter<ChartValueFormat>(),
            new JsonStringEnumConverter()
        }
    };

    [Fact]
    public void ChartSpec_SerializesKindAndValueFormatInLowerCase()
    {
        var chart = new ChartSpecDto(
            ChartKind.Bar,
            "Pedidos por região",
            ["Moema", "Santana"],
            [new ChartSeriesDto("Pedidos", [42, 24])],
            ChartValueFormat.Number);

        var json = JsonSerializer.Serialize(chart, ApiJsonOptions);

        Assert.Contains("\"kind\":\"bar\"", json);
        Assert.Contains("\"valueFormat\":\"number\"", json);
        Assert.Contains("\"labels\":[\"Moema\",\"Santana\"]", json);
    }

    [Fact]
    public void AnswerBlock_SerializesTypeInLowerCase()
    {
        var block = new AnswerBlockDto(
            AnswerBlockType.Kpis,
            Kpis: [new AnswerKpiDto("Faturamento", "2380.50", ChartValueFormat.Currency)]);

        var json = JsonSerializer.Serialize(block, ApiJsonOptions);

        Assert.Contains("\"type\":\"kpis\"", json);
        Assert.Contains("\"valueFormat\":\"currency\"", json);
    }

    [Fact]
    public void ChatAnswer_ExposesConversationMessageAndUsage()
    {
        var answer = new ChatAnswerDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Resposta",
            [new AnswerBlockDto(AnswerBlockType.Text, Text: "Resposta")],
            new AiMetaDto("Gemini", "gemini-1.5-flash", "setembro/2026", 1200, ["get_orders_by_region"], false),
            new AiUsageDto(100, 20, 0.0001m, 100, 20, 0.0001m, 5m, 0.002m, false, false));

        var json = JsonSerializer.Serialize(answer, ApiJsonOptions);

        Assert.Contains("\"conversationId\":", json);
        Assert.Contains("\"messageId\":", json);
        Assert.Contains("\"usage\":", json);
        Assert.Contains("\"toolsUsed\":[\"get_orders_by_region\"]", json);
    }

    [Fact]
    public void OrdersByRegionItem_WithoutRegion_FallsBackToNotClassified()
    {
        var item = new OrdersByRegionItemDto(
            null, "Bairro antigo", null, 3, 2, 120m, 40m, 10m);

        Assert.Equal("Não classificado", item.RegionLabel);
    }
}

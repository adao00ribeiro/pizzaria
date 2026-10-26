using System.Text.Json;
using System.Text.Json.Serialization;
using PizzariaLucrativa.Application.Serialization;
using PizzariaLucrativa.Domain.Enums;

namespace PizzariaLucrativa.Application.DTOs;

// ================================================================ Chat

public sealed record ChatRequestDto(string Message, Guid? ConversationId = null);

/// <summary>Mensagem trocada com o assistente (também usada pelo store de histórico).</summary>
public sealed record ChatMessageDto(
    AiMessageRole Role,
    string Content,
    IReadOnlyList<string>? ToolsUsed = null,
    int InputTokens = 0,
    int OutputTokens = 0,
    decimal EstimatedCostUsd = 0m);

public sealed record ChatAnswerDto(
    Guid ConversationId,
    Guid MessageId,
    string Text,
    IReadOnlyList<AnswerBlockDto> Blocks,
    AiMetaDto Meta,
    AiUsageDto Usage);

// ================================================================ Blocos de resposta

[JsonConverter(typeof(LowerCaseEnumConverter<AnswerBlockType>))]
public enum AnswerBlockType
{
    Text = 0,
    Chart = 1,
    Table = 2,
    Kpis = 3
}

/// <summary>
/// Bloco renderizável da resposta. Exatamente um payload é preenchido conforme <see cref="Type"/>.
/// Gráficos só são montados no backend a partir do resultado de uma tool; o modelo nunca envia números.
/// </summary>
public sealed record AnswerBlockDto(
    AnswerBlockType Type,
    string? Text = null,
    ChartSpecDto? Chart = null,
    TableBlockDto? Table = null,
    IReadOnlyList<AnswerKpiDto>? Kpis = null);

public sealed record AnswerKpiDto(
    string Label,
    string Value,
    ChartValueFormat ValueFormat = ChartValueFormat.Number);

// ================================================================ ChartSpec

[JsonConverter(typeof(LowerCaseEnumConverter<ChartKind>))]
public enum ChartKind
{
    Bar = 0,
    Line = 1,
    Pie = 2,
    Doughnut = 3
}

[JsonConverter(typeof(LowerCaseEnumConverter<ChartValueFormat>))]
public enum ChartValueFormat
{
    Currency = 0,
    Number = 1,
    Percent = 2
}

public sealed record ChartSeriesDto(string Name, IReadOnlyList<decimal> Data);

/// <summary>
/// Especificação de gráfico montada exclusivamente pelo backend (IA-3).
/// O modelo de linguagem não preenche <see cref="Labels"/> nem <see cref="Series"/>.
/// </summary>
public sealed record ChartSpecDto(
    ChartKind Kind,
    string Title,
    IReadOnlyList<string> Labels,
    IReadOnlyList<ChartSeriesDto> Series,
    ChartValueFormat ValueFormat);

public sealed record TableBlockDto(
    string? Title,
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<string>> Rows);

// ================================================================ Metadados e uso

public sealed record AiMetaDto(
    string Provider,
    string Model,
    string? PeriodLabel,
    int LatencyMs,
    IReadOnlyList<string> ToolsUsed,
    bool IsFallback);

public sealed record AiUsageDto(
    int InputTokens,
    int OutputTokens,
    decimal EstimatedCostUsd,
    int MonthlyInputTokens,
    int MonthlyOutputTokens,
    decimal MonthlyCostUsd,
    decimal MonthlyBudgetUsd,
    decimal BudgetUsedPercentage,
    bool BudgetExceeded,
    bool IsFallback);

/// <summary>Consumo de uma chamada ao provedor, registrado independentemente do histórico.</summary>
public sealed record AiUsageEntryDto(
    Guid? ConversationId,
    string Provider,
    string Model,
    int InputTokens,
    int OutputTokens,
    decimal EstimatedCostUsd,
    int LatencyMs,
    bool IsFallback);

// ================================================================ Histórico

public sealed record ConversationSummaryDto(
    Guid Id,
    string Title,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int MessageCount,
    bool IsArchived);

public sealed record ConversationMessageDto(
    Guid Id,
    AiMessageRole Role,
    string Content,
    DateTime CreatedAt,
    IReadOnlyList<string> ToolsUsed,
    decimal EstimatedCostUsd);

public sealed record ConversationDetailDto(
    Guid Id,
    string Title,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool IsArchived,
    IReadOnlyList<ConversationMessageDto> Messages);

// ================================================================ Contrato do provedor

/// <summary>Declaração de tool no formato aceito pelo adapter do provedor.</summary>
public sealed record ChatToolDeclaration(
    string Name,
    string Description,
    JsonElement Parameters);

public sealed record ChatToolCall(string Id, string Name, JsonElement Arguments);

/// <summary>
/// Mensagem no protocolo do modelo. <see cref="Role"/> aceita "user", "model" ou "tool";
/// respostas de tool usam <see cref="ToolName"/> e <see cref="ToolResponse"/>.
/// </summary>
public sealed record ChatModelMessage(
    string Role,
    string? Content = null,
    IReadOnlyList<ChatToolCall>? ToolCalls = null,
    string? ToolName = null,
    JsonElement? ToolResponse = null);

public sealed record ChatCompletionRequest(
    string SystemPrompt,
    IReadOnlyList<ChatModelMessage> Messages,
    IReadOnlyList<ChatToolDeclaration> Tools,
    int MaxOutputTokens = 1024,
    double Temperature = 0.2);

public sealed record ChatCompletionResult(
    string? Text,
    IReadOnlyList<ChatToolCall> ToolCalls,
    int InputTokens,
    int OutputTokens,
    string Provider,
    string Model,
    int LatencyMs,
    bool IsFallback);

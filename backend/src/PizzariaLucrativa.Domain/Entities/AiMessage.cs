using PizzariaLucrativa.Domain.Enums;

namespace PizzariaLucrativa.Domain.Entities;

/// <summary>
/// Mensagem persistida de uma conversa do assistente (docs/sprints, IA-1/IA-3).
/// Guarda apenas texto e metadados de consumo; payload bruto de tool não é persistido por padrão.
/// </summary>
public class AiMessage : TenantEntity
{
    public Guid ConversationId { get; set; }
    public AiConversation? Conversation { get; set; }

    public AiMessageRole Role { get; set; }

    /// <summary>Conteúdo textual, limitado a 8.000 caracteres pela persistência.</summary>
    public string Content { get; set; } = string.Empty;

    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }

    /// <summary>Custo estimado da mensagem em dólares (nunca usado para cobrança).</summary>
    public decimal EstimatedCostUsd { get; set; }

    /// <summary>Nomes das tools usadas, separados por vírgula (máx. 500 caracteres).</summary>
    public string? ToolsUsed { get; set; }
}

namespace PizzariaLucrativa.Domain.Entities;

/// <summary>
/// Registro de consumo do provedor de IA por tenant (docs/sprints, IA-1/IA-3).
/// Usado para orçamento mensal e observabilidade; nunca guarda prompt ou conteúdo de mensagem.
/// </summary>
public class AiUsageRecord : TenantEntity
{
    /// <summary>Usuário que disparou a chamada (claim do token).</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Conversa relacionada, quando o histórico está habilitado.</summary>
    public Guid? ConversationId { get; set; }
    public AiConversation? Conversation { get; set; }

    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;

    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public decimal EstimatedCostUsd { get; set; }

    public int LatencyMs { get; set; }

    /// <summary>Indica resposta de fallback seguro (timeout, 429, orçamento etc.).</summary>
    public bool IsFallback { get; set; }
}

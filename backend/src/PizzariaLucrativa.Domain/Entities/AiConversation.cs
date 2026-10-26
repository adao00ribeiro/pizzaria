namespace PizzariaLucrativa.Domain.Entities;

/// <summary>
/// Conversa do assistente de IA persistida por tenant (docs/sprints, IA-1/IA-3).
/// O histórico é sempre owner-only e nunca armazena payload bruto de tool ou PII de cliente.
/// </summary>
public class AiConversation : TenantEntity
{
    /// <summary>Usuário dono da conversa (claim do token).</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Título curto exibido na listagem de conversas.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Arquiva sem excluir; conversas arquivadas não aparecem na listagem padrão.</summary>
    public bool IsArchived { get; set; }

    public List<AiMessage> Messages { get; set; } = new();
}

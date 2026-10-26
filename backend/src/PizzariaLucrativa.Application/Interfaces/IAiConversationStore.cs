using PizzariaLucrativa.Application.DTOs;

namespace PizzariaLucrativa.Application.Interfaces;

/// <summary>
/// Persistência do histórico do assistente por tenant (docs/sprints, IA-1/IA-3).
/// O escopo é sempre o tenant/owner do token; ids de outro tenant devem resultar em 404.
/// </summary>
public interface IAiConversationStore
{
    /// <summary>Retoma a conversa informada ou cria uma nova (título derivado da pergunta).</summary>
    Task<ConversationDetailDto> GetOrCreateAsync(
        Guid? conversationId, string title, CancellationToken ct = default);

    Task<IReadOnlyList<ConversationSummaryDto>> ListAsync(
        bool includeArchived = false, CancellationToken ct = default);

    Task<ConversationDetailDto> GetAsync(Guid conversationId, CancellationToken ct = default);

    Task<ConversationMessageDto> AppendMessageAsync(
        Guid conversationId, ChatMessageDto message, CancellationToken ct = default);

    Task DeleteAsync(Guid conversationId, CancellationToken ct = default);

    /// <summary>Remove conversas anteriores ao limite de retenção e devolve a quantidade removida.</summary>
    Task<int> PurgeExpiredAsync(DateTime olderThanUtc, CancellationToken ct = default);
}

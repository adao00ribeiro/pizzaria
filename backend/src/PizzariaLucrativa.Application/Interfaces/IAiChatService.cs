using PizzariaLucrativa.Application.DTOs;

namespace PizzariaLucrativa.Application.Interfaces;

/// <summary>
/// Orquestra a pergunta do owner: tools permitidas, orçamento, histórico e resposta segura
/// (docs/sprints, IA-3). Nenhum número é aceito fora do resultado de uma tool.
/// </summary>
public interface IAiChatService
{
    Task<ChatAnswerDto> AskAsync(ChatRequestDto request, CancellationToken ct = default);
}

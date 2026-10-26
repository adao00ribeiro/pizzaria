namespace PizzariaLucrativa.Domain.Enums;

/// <summary>
/// Papel da mensagem no histórico persistido do assistente (docs/sprints, IA-1).
/// Respostas de tool não são persistidas como mensagem por padrão (ver <c>Ai:History:StoreToolResults</c>).
/// </summary>
public enum AiMessageRole
{
    User = 0,
    Assistant = 1
}

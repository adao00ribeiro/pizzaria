using PizzariaLucrativa.Application.DTOs;

namespace PizzariaLucrativa.Application.Interfaces;

/// <summary>
/// Abstração do provedor de LLM (Gemini no MVP). A Application não conhece SDK nem HTTP;
/// a implementação concreta fica na Infrastructure e é trocável por configuração.
/// </summary>
public interface IChatModelClient
{
    Task<ChatCompletionResult> CompleteAsync(
        ChatCompletionRequest request, CancellationToken ct = default);
}

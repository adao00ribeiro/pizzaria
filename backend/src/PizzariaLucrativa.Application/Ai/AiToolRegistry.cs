using PizzariaLucrativa.Application.DTOs;

namespace PizzariaLucrativa.Application.Ai;

/// <summary>
/// Registro central de tools permitidas para function calling no Gemini.
/// Valida allowlist e expõe declarações para o modelo.
/// </summary>
public sealed class AiToolRegistry
{
    private readonly Dictionary<string, ChatToolDeclaration> _tools;

    public AiToolRegistry()
    {
        _tools = AiToolDefinitions.All.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
    }

    public bool IsAllowed(string toolName)
    {
        return !string.IsNullOrWhiteSpace(toolName) && _tools.ContainsKey(toolName);
    }

    public ChatToolDeclaration? GetByName(string toolName)
    {
        return _tools.TryGetValue(toolName, out var declaration) ? declaration : null;
    }

    public IReadOnlyList<ChatToolDeclaration> GetAllDeclarations()
    {
        return AiToolDefinitions.All;
    }
}

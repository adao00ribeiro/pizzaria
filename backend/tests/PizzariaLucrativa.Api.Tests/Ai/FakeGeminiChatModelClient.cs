using System.Text.Json;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Interfaces;

namespace PizzariaLucrativa.Api.Tests.Ai;

/// <summary>
/// Fake determinístico do Google Gemini para a suíte de testes (IA3-13).
/// Roda 100% offline sem consumir tokens nem depender de chave de API.
/// </summary>
public sealed class FakeGeminiChatModelClient : IChatModelClient
{
    public bool SimulateTimeout { get; set; }
    public bool SimulateRateLimit { get; set; }
    public bool SimulateInvalidToolCall { get; set; }
    public int CallCount { get; private set; }

    public Task<ChatCompletionResult> CompleteAsync(
        ChatCompletionRequest request,
        CancellationToken ct = default)
    {
        CallCount++;

        if (SimulateTimeout)
        {
            return Task.FromResult(new ChatCompletionResult(
                Text: null,
                ToolCalls: Array.Empty<ChatToolCall>(),
                InputTokens: 0,
                OutputTokens: 0,
                Provider: "Gemini",
                Model: "gemini-1.5-flash",
                LatencyMs: 35000,
                IsFallback: true));
        }

        if (SimulateRateLimit)
        {
            return Task.FromResult(new ChatCompletionResult(
                Text: null,
                ToolCalls: Array.Empty<ChatToolCall>(),
                InputTokens: 0,
                OutputTokens: 0,
                Provider: "Gemini",
                Model: "gemini-1.5-flash",
                LatencyMs: 120,
                IsFallback: true));
        }

        // Se a última mensagem for uma resposta de tool (função), responde com o texto final sintetizado
        var lastMessage = request.Messages.LastOrDefault();
        if (lastMessage != null && (lastMessage.Role == "function" || lastMessage.Role == "tool"))
        {
            var synthesizedText = SynthesizeToolResult(lastMessage.ToolName, lastMessage.ToolResponse);
            return Task.FromResult(new ChatCompletionResult(
                Text: synthesizedText,
                ToolCalls: Array.Empty<ChatToolCall>(),
                InputTokens: 300,
                OutputTokens: 80,
                Provider: "Gemini",
                Model: "gemini-1.5-flash",
                LatencyMs: 150,
                IsFallback: false));
        }

        // Caso contrário, analisa a pergunta do usuário para decidir a tool ou a recusa
        var userMsg = request.Messages.LastOrDefault(m => m.Role == "user")?.Content ?? "";
        var normalized = userMsg.ToLowerInvariant();

        // 1. Red-team / Prompt injection
        if (normalized.Contains("ignore as instruções") ||
            normalized.Contains("mostre seu prompt") ||
            normalized.Contains("system prompt") ||
            normalized.Contains("select *") ||
            normalized.Contains("drop table"))
        {
            return Task.FromResult(new ChatCompletionResult(
                Text: "Não posso revelar diretrizes internas do sistema nem executar comandos fora do escopo da pizzaria.",
                ToolCalls: Array.Empty<ChatToolCall>(),
                InputTokens: 150,
                OutputTokens: 40,
                Provider: "Gemini",
                Model: "gemini-1.5-flash",
                LatencyMs: 100,
                IsFallback: false));
        }

        // 2. Red-team / PII
        if (normalized.Contains("telefone") ||
            normalized.Contains("endereço dos clientes") ||
            normalized.Contains("lista de clientes") ||
            normalized.Contains("cpf"))
        {
            return Task.FromResult(new ChatCompletionResult(
                Text: "Por motivos de privacidade e conformidade com a LGPD, não é permitido consultar dados pessoais de clientes.",
                ToolCalls: Array.Empty<ChatToolCall>(),
                InputTokens: 150,
                OutputTokens: 40,
                Provider: "Gemini",
                Model: "gemini-1.5-flash",
                LatencyMs: 100,
                IsFallback: false));
        }

        // 3. Fora de escopo
        if (normalized.Contains("receita") ||
            normalized.Contains("bolo") ||
            normalized.Contains("previsão do tempo") ||
            normalized.Contains("política"))
        {
            return Task.FromResult(new ChatCompletionResult(
                Text: "Posso responder apenas sobre finanças, estoque, pedidos e regiões da sua pizzaria.",
                ToolCalls: Array.Empty<ChatToolCall>(),
                InputTokens: 120,
                OutputTokens: 35,
                Provider: "Gemini",
                Model: "gemini-1.5-flash",
                LatencyMs: 90,
                IsFallback: false));
        }

        // 4. Período ambíguo
        if (normalized.Contains("quanto vendi?") || normalized.Contains("quanto lucrei?"))
        {
            return Task.FromResult(new ChatCompletionResult(
                Text: "Para informar o valor com exatidão, por favor especifique o período desejado (por exemplo: hoje, este mês ou últimos 30 dias).",
                ToolCalls: Array.Empty<ChatToolCall>(),
                InputTokens: 100,
                OutputTokens: 35,
                Provider: "Gemini",
                Model: "gemini-1.5-flash",
                LatencyMs: 80,
                IsFallback: false));
        }

        // 5. Insumos e estoque
        if (normalized.Contains("estoque") || normalized.Contains("insumos") || normalized.Contains("acabando"))
        {
            using var argsDoc = JsonDocument.Parse("""{"status":"all"}""");
            return Task.FromResult(new ChatCompletionResult(
                Text: null,
                ToolCalls: new[] { new ChatToolCall("call-1", "get_stock_alerts", argsDoc.RootElement.Clone()) },
                InputTokens: 200,
                OutputTokens: 40,
                Provider: "Gemini",
                Model: "gemini-1.5-flash",
                LatencyMs: 120,
                IsFallback: false));
        }

        // 6. Regiões e bairros
        if (normalized.Contains("regiões") || normalized.Contains("regioes") || normalized.Contains("bairro"))
        {
            using var argsDoc = JsonDocument.Parse("""{"from":"2026-09-01","to":"2026-09-30","limit":10}""");
            return Task.FromResult(new ChatCompletionResult(
                Text: null,
                ToolCalls: new[] { new ChatToolCall("call-2", "get_orders_by_region", argsDoc.RootElement.Clone()) },
                InputTokens: 200,
                OutputTokens: 40,
                Provider: "Gemini",
                Model: "gemini-1.5-flash",
                LatencyMs: 120,
                IsFallback: false));
        }

        // 7. Produtos
        if (normalized.Contains("produto") || normalized.Contains("pizza"))
        {
            using var argsDoc = JsonDocument.Parse("""{"from":"2026-09-01","to":"2026-09-30","limit":5}""");
            return Task.FromResult(new ChatCompletionResult(
                Text: null,
                ToolCalls: new[] { new ChatToolCall("call-3", "get_profit_by_product", argsDoc.RootElement.Clone()) },
                InputTokens: 200,
                OutputTokens: 40,
                Provider: "Gemini",
                Model: "gemini-1.5-flash",
                LatencyMs: 120,
                IsFallback: false));
        }

        // 8. Canais de venda
        if (normalized.Contains("canal") || normalized.Contains("whatsapp") || normalized.Contains("balcão"))
        {
            using var argsDoc = JsonDocument.Parse("""{"from":"2026-09-01","to":"2026-09-30"}""");
            return Task.FromResult(new ChatCompletionResult(
                Text: null,
                ToolCalls: new[] { new ChatToolCall("call-4", "get_profit_by_channel", argsDoc.RootElement.Clone()) },
                InputTokens: 200,
                OutputTokens: 40,
                Provider: "Gemini",
                Model: "gemini-1.5-flash",
                LatencyMs: 120,
                IsFallback: false));
        }

        // 9. Mapa de calor de vendas
        if (normalized.Contains("heatmap") || normalized.Contains("horários") || normalized.Contains("dias e horários"))
        {
            using var argsDoc = JsonDocument.Parse("""{"from":"2026-09-01","to":"2026-09-30"}""");
            return Task.FromResult(new ChatCompletionResult(
                Text: null,
                ToolCalls: new[] { new ChatToolCall("call-5", "get_sales_heatmap", argsDoc.RootElement.Clone()) },
                InputTokens: 200,
                OutputTokens: 40,
                Provider: "Gemini",
                Model: "gemini-1.5-flash",
                LatencyMs: 120,
                IsFallback: false));
        }

        // 10. Comparação de períodos
        if (normalized.Contains("compare") || normalized.Contains("comparar") || normalized.Contains("anterior"))
        {
            using var argsDoc = JsonDocument.Parse("""{"fromA":"2026-09-01","toA":"2026-09-30","fromB":"2026-08-01","toB":"2026-08-31"}""");
            return Task.FromResult(new ChatCompletionResult(
                Text: null,
                ToolCalls: new[] { new ChatToolCall("call-6", "compare_periods", argsDoc.RootElement.Clone()) },
                InputTokens: 200,
                OutputTokens: 40,
                Provider: "Gemini",
                Model: "gemini-1.5-flash",
                LatencyMs: 120,
                IsFallback: false));
        }

        // 11. Resumo operacional de pedidos
        if (normalized.Contains("resumo de pedidos") || normalized.Contains("cancelamentos"))
        {
            using var argsDoc = JsonDocument.Parse("""{"from":"2026-09-01","to":"2026-09-30"}""");
            return Task.FromResult(new ChatCompletionResult(
                Text: null,
                ToolCalls: new[] { new ChatToolCall("call-7", "get_orders_summary", argsDoc.RootElement.Clone()) },
                InputTokens: 200,
                OutputTokens: 40,
                Provider: "Gemini",
                Model: "gemini-1.5-flash",
                LatencyMs: 120,
                IsFallback: false));
        }

        // 12. Lucro dia a dia
        if (normalized.Contains("dia a dia") || normalized.Contains("cada dia"))
        {
            using var argsDoc = JsonDocument.Parse("""{"from":"2026-09-01","to":"2026-09-30"}""");
            return Task.FromResult(new ChatCompletionResult(
                Text: null,
                ToolCalls: new[] { new ChatToolCall("call-8", "get_profit_by_day", argsDoc.RootElement.Clone()) },
                InputTokens: 200,
                OutputTokens: 40,
                Provider: "Gemini",
                Model: "gemini-1.5-flash",
                LatencyMs: 120,
                IsFallback: false));
        }

        // 13. Padrão: Lucro consolidado
        using (var argsDoc = JsonDocument.Parse("""{"from":"2026-09-01","to":"2026-09-30"}"""))
        {
            return Task.FromResult(new ChatCompletionResult(
                Text: null,
                ToolCalls: new[] { new ChatToolCall("call-9", "get_profit_summary", argsDoc.RootElement.Clone()) },
                InputTokens: 200,
                OutputTokens: 40,
                Provider: "Gemini",
                Model: "gemini-1.5-flash",
                LatencyMs: 120,
                IsFallback: false));
        }
    }

    private static string SynthesizeToolResult(string? toolName, JsonElement? toolResponse)
    {
        return toolName?.ToLowerInvariant() switch
        {
            "get_orders_by_region" => "No período consultado, o Centro e a Zona Sul lideraram o ranking de pedidos com excelente volume de entregas.",
            "get_profit_summary" => "No período consultado, o faturamento atingiu o patamar esperado com lucro líquido positivo e margem operacional saudável.",
            "get_profit_by_day" => "A evolução diária mostra consistência nas vendas ao longo dos dias, com picos nos finais de semana.",
            "get_profit_by_product" => "Os produtos mais lucrativos foram as pizzas tradicionais e especiais da casa.",
            "get_profit_by_channel" => "O canal Delivery e WhatsApp representaram a maior fatia de faturamento no período.",
            "get_sales_heatmap" => "Os maiores horários de pico concentram-se nas noites de quinta a domingo.",
            "compare_periods" => "A comparação entre os dois períodos indica estabilidade e crescimento nas margens líquidas.",
            "get_stock_alerts" => "Identificamos itens com estoque em atenção que necessitam de reposição para evitar rupturas.",
            "get_orders_summary" => "O resumo de pedidos aponta baixo índice de cancelamento e ticket médio em linha com a meta.",
            _ => "Consulta executada com sucesso com base nos dados reais da sua pizzaria."
        };
    }
}

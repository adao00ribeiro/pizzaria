using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PizzariaLucrativa.Application.Ai;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Exceptions;
using PizzariaLucrativa.Application.Interfaces;
using PizzariaLucrativa.Application.Options;
using PizzariaLucrativa.Domain.Enums;

namespace PizzariaLucrativa.Application.Services;

/// <summary>
/// Orquestra a interação do owner com o assistente Gemini (IA3-07).
/// Executa tools em loop, gera ChartSpec no backend a partir dos dados reais e persiste mensagens.
/// </summary>
public sealed class AiChatService : IAiChatService
{
    private readonly IChatModelClient _chatClient;
    private readonly AiToolRegistry _toolRegistry;
    private readonly AiToolExecutor _toolExecutor;
    private readonly AiPromptBuilder _promptBuilder;
    private readonly IAiConversationStore _conversationStore;
    private readonly IAiUsageService _usageService;
    private readonly IMemoryCache _memoryCache;
    private readonly AiOptions _options;
    private readonly ILogger<AiChatService> _logger;

    public AiChatService(
        IChatModelClient chatClient,
        AiToolRegistry toolRegistry,
        AiToolExecutor toolExecutor,
        AiPromptBuilder promptBuilder,
        IAiConversationStore conversationStore,
        IAiUsageService usageService,
        IMemoryCache memoryCache,
        IOptions<AiOptions> options,
        ILogger<AiChatService> logger)
    {
        _chatClient = chatClient;
        _toolRegistry = toolRegistry;
        _toolExecutor = toolExecutor;
        _promptBuilder = promptBuilder;
        _conversationStore = conversationStore;
        _usageService = usageService;
        _memoryCache = memoryCache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ChatAnswerDto> AskAsync(ChatRequestDto request, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            throw new ValidationException("Message", "A pergunta não pode ser vazia.");
        }

        if (request.Message.Length > 2_000)
        {
            throw new ValidationException("Message", "A mensagem excede o limite máximo de 2.000 caracteres.");
        }

        // 1. Kill switch
        if (!_options.Enabled)
        {
            sw.Stop();
            var disabledUsage = await _usageService.GetUsageAsync(ct);
            return BuildFallbackAnswer(
                request.ConversationId ?? Guid.NewGuid(),
                "O assistente de IA está desabilitado no momento. Solicite a ativação nas configurações.",
                (int)sw.ElapsedMilliseconds,
                disabledUsage);
        }

        // 2. Verificação de orçamento antes de chamar o Gemini
        const decimal estimatedCallCost = 0.001m;
        var canSpend = await _usageService.CanSpendAsync(estimatedCallCost, ct);
        if (!canSpend)
        {
            sw.Stop();
            await _usageService.RecordAsync(new AiUsageEntryDto(
                request.ConversationId,
                _options.Provider,
                _options.Model,
                0,
                0,
                0m,
                (int)sw.ElapsedMilliseconds,
                IsFallback: true), ct);

            var currentUsage = await _usageService.GetUsageAsync(ct);
            return BuildFallbackAnswer(
                request.ConversationId ?? Guid.NewGuid(),
                "O limite de orçamento mensal para uso do assistente foi atingido neste mês. Para continuar utilizando, ajuste o teto nas configurações ou aguarde o próximo ciclo.",
                (int)sw.ElapsedMilliseconds,
                currentUsage);
        }

        // 3. Cache de perguntas idênticas
        var cacheKey = $"ai_chat_{request.ConversationId}_{request.Message.Trim().ToLowerInvariant()}";
        if (_options.CacheMinutes > 0 && _memoryCache.TryGetValue(cacheKey, out ChatAnswerDto? cached) && cached != null)
        {
            return cached;
        }

        // 4. Carrega ou cria conversa
        var conversationTitle = request.Message.Length > 50 ? request.Message[..50] + "..." : request.Message;
        var conversation = await _conversationStore.GetOrCreateAsync(request.ConversationId, conversationTitle, ct);
        var conversationId = conversation.Id;

        // 5. Monta mensagens de histórico + pergunta atual
        var systemPrompt = _promptBuilder.BuildSystemPrompt();
        var modelMessages = new List<ChatModelMessage>();

        if (conversation.Messages.Count > 0)
        {
            var historySlice = conversation.Messages
                .TakeLast(_options.MaxHistoryMessages)
                .ToList();

            foreach (var h in historySlice)
            {
                var role = h.Role == AiMessageRole.Assistant ? "model" : "user";
                modelMessages.Add(new ChatModelMessage(role, h.Content));
            }
        }

        modelMessages.Add(new ChatModelMessage("user", AiPromptBuilder.WrapUserQuery(request.Message)));

        // 6. Loop de Function Calling (máximo de rodadas configurável)
        var executedTools = new List<string>();
        var executedToolResults = new List<(string Name, JsonElement Args, JsonElement Result)>();
        var totalInputTokens = 0;
        var totalOutputTokens = 0;
        string? finalText = null;
        var isFallback = false;

        var availableTools = _toolRegistry.GetAllDeclarations();

        for (var round = 0; round < _options.MaxToolRounds; round++)
        {
            var completionReq = new ChatCompletionRequest(
                systemPrompt,
                modelMessages,
                availableTools,
                _options.MaxOutputTokens,
                Temperature: 0.2);

            var completion = await _chatClient.CompleteAsync(completionReq, ct);

            totalInputTokens += completion.InputTokens;
            totalOutputTokens += completion.OutputTokens;

            if (completion.IsFallback)
            {
                isFallback = true;
                break;
            }

            if (completion.ToolCalls.Count > 0)
            {
                modelMessages.Add(new ChatModelMessage("model", completion.Text, completion.ToolCalls));

                foreach (var toolCall in completion.ToolCalls)
                {
                    if (_toolRegistry.IsAllowed(toolCall.Name))
                    {
                        executedTools.Add(toolCall.Name);

                        var toolResult = await _toolExecutor.ExecuteAsync(toolCall.Name, toolCall.Arguments, ct);
                        executedToolResults.Add((toolCall.Name, toolCall.Arguments, toolResult));

                        modelMessages.Add(new ChatModelMessage(
                            "function",
                            ToolName: toolCall.Name,
                            ToolResponse: toolResult));
                    }
                    else
                    {
                        using var errDoc = JsonDocument.Parse("""{"error":"Tool não permitida."}""");
                        modelMessages.Add(new ChatModelMessage(
                            "function",
                            ToolName: toolCall.Name,
                            ToolResponse: errDoc.RootElement.Clone()));
                    }
                }
            }
            else
            {
                finalText = completion.Text;
                break;
            }
        }

        sw.Stop();
        var latencyMs = (int)sw.ElapsedMilliseconds;

        // Custo estimado em dólares (Gemini 1.5 Flash: ~$0.075/1M input, ~$0.30/1M output)
        var estimatedCostUsd = Math.Round(
            (totalInputTokens * 0.000000075m) + (totalOutputTokens * 0.00000030m),
            6);

        // Fallback seguro se o modelo não gerou texto ou falhou
        if (isFallback || string.IsNullOrWhiteSpace(finalText))
        {
            finalText = "Não foi possível obter uma resposta precisa no momento. " +
                        "Experimente reformular a pergunta ou especificar um período (ex.: 'este mês', 'últimos 30 dias').";
            isFallback = true;
        }

        // 7. Monta blocos e ChartSpec exclusivamente a partir do resultado das tools
        var blocks = BuildAnswerBlocks(finalText, executedToolResults);

        // 8. Persiste mensagens se histórico estiver ativo
        var messageId = Guid.NewGuid();
        if (_options.History.Enabled)
        {
            await _conversationStore.AppendMessageAsync(
                conversationId,
                new ChatMessageDto(AiMessageRole.User, request.Message),
                ct);

            var savedAssistantMsg = await _conversationStore.AppendMessageAsync(
                conversationId,
                new ChatMessageDto(
                    AiMessageRole.Assistant,
                    finalText,
                    executedTools.Distinct().ToList(),
                    totalInputTokens,
                    totalOutputTokens,
                    estimatedCostUsd),
                ct);

            messageId = savedAssistantMsg.Id;
        }

        // 9. Registra consumo
        await _usageService.RecordAsync(new AiUsageEntryDto(
            conversationId,
            _options.Provider,
            _options.Model,
            totalInputTokens,
            totalOutputTokens,
            estimatedCostUsd,
            latencyMs,
            isFallback), ct);

        var finalUsage = await _usageService.GetUsageAsync(ct);

        var meta = new AiMetaDto(
            Provider: _options.Provider,
            Model: _options.Model,
            PeriodLabel: ResolvePeriodLabel(executedToolResults),
            LatencyMs: latencyMs,
            ToolsUsed: executedTools.Distinct().ToList(),
            IsFallback: isFallback);

        var responseDto = new ChatAnswerDto(
            conversationId,
            messageId,
            finalText,
            blocks,
            meta,
            finalUsage);

        if (_options.CacheMinutes > 0 && !isFallback)
        {
            _memoryCache.Set(cacheKey, responseDto, TimeSpan.FromMinutes(_options.CacheMinutes));
        }

        return responseDto;
    }

    private static IReadOnlyList<AnswerBlockDto> BuildAnswerBlocks(
        string text,
        IReadOnlyList<(string Name, JsonElement Args, JsonElement Result)> toolResults)
    {
        var blocks = new List<AnswerBlockDto>
        {
            new(AnswerBlockType.Text, Text: text)
        };

        foreach (var (name, _, result) in toolResults)
        {
            switch (name.ToLowerInvariant())
            {
                case "get_orders_by_region":
                    var regionChart = BuildRegionChart(result);
                    if (regionChart != null)
                        blocks.Add(new AnswerBlockDto(AnswerBlockType.Chart, Chart: regionChart));
                    break;

                case "get_profit_by_day":
                    var profitChart = BuildDailyProfitChart(result);
                    if (profitChart != null)
                        blocks.Add(new AnswerBlockDto(AnswerBlockType.Chart, Chart: profitChart));
                    break;

                case "get_profit_by_product":
                    var productChart = BuildProductProfitChart(result);
                    if (productChart != null)
                        blocks.Add(new AnswerBlockDto(AnswerBlockType.Chart, Chart: productChart));
                    break;

                case "get_profit_summary":
                    var kpis = BuildProfitKpis(result);
                    if (kpis != null && kpis.Count > 0)
                        blocks.Add(new AnswerBlockDto(AnswerBlockType.Kpis, Kpis: kpis));
                    break;
            }
        }

        return blocks;
    }

    private static ChartSpecDto? BuildRegionChart(JsonElement result)
    {
        try
        {
            if (!result.TryGetProperty("items", out var items) || items.GetArrayLength() == 0)
            {
                return null;
            }

            var labels = new List<string>();
            var orderCounts = new List<decimal>();

            foreach (var item in items.EnumerateArray())
            {
                var label = (item.TryGetProperty("district", out var dst) ? dst.GetString() : null)
                    ?? (item.TryGetProperty("regionLabel", out var rl) ? rl.GetString() : null)
                    ?? "Outros";

                var orders = (item.TryGetProperty("ordersCount", out var oc1) && oc1.TryGetDecimal(out var val1))
                    ? val1
                    : (item.TryGetProperty("orderCount", out var oc2) && oc2.TryGetDecimal(out var val2))
                        ? val2
                        : 0m;

                labels.Add(label);
                orderCounts.Add(orders);
            }

            return new ChartSpecDto(
                ChartKind.Bar,
                "Pedidos por Região",
                labels,
                new List<ChartSeriesDto> { new("Pedidos", orderCounts) },
                ChartValueFormat.Number);
        }
        catch
        {
            return null;
        }
    }

    private static ChartSpecDto? BuildDailyProfitChart(JsonElement result)
    {
        try
        {
            if (result.ValueKind != JsonValueKind.Array || result.GetArrayLength() == 0)
            {
                return null;
            }

            var labels = new List<string>();
            var profits = new List<decimal>();

            foreach (var item in result.EnumerateArray())
            {
                var dateStr = item.TryGetProperty("date", out var d) ? d.GetString() ?? "" : "";
                var netProfit = item.TryGetProperty("netProfit", out var p) ? p.GetDecimal() : 0m;

                labels.Add(dateStr);
                profits.Add(netProfit);
            }

            return new ChartSpecDto(
                ChartKind.Line,
                "Lucro Diário",
                labels,
                new List<ChartSeriesDto> { new("Lucro Líquido", profits) },
                ChartValueFormat.Currency);
        }
        catch
        {
            return null;
        }
    }

    private static ChartSpecDto? BuildProductProfitChart(JsonElement result)
    {
        try
        {
            if (result.ValueKind != JsonValueKind.Array || result.GetArrayLength() == 0)
            {
                return null;
            }

            var labels = new List<string>();
            var profits = new List<decimal>();

            foreach (var item in result.EnumerateArray())
            {
                var name = item.TryGetProperty("productName", out var n) ? n.GetString() ?? "" : "";
                var grossProfit = item.TryGetProperty("grossProfit", out var gp) ? gp.GetDecimal() : 0m;

                labels.Add(name);
                profits.Add(grossProfit);
            }

            return new ChartSpecDto(
                ChartKind.Bar,
                "Lucro por Produto",
                labels,
                new List<ChartSeriesDto> { new("Lucro Bruto", profits) },
                ChartValueFormat.Currency);
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyList<AnswerKpiDto>? BuildProfitKpis(JsonElement result)
    {
        try
        {
            var kpis = new List<AnswerKpiDto>();

            if (result.TryGetProperty("grossRevenue", out var gr))
                kpis.Add(new AnswerKpiDto("Faturamento Bruto", gr.GetDecimal().ToString("C2", new System.Globalization.CultureInfo("pt-BR")), ChartValueFormat.Currency));

            if (result.TryGetProperty("grossProfit", out var gp))
                kpis.Add(new AnswerKpiDto("Lucro Bruto", gp.GetDecimal().ToString("C2", new System.Globalization.CultureInfo("pt-BR")), ChartValueFormat.Currency));

            if (result.TryGetProperty("netProfit", out var np))
                kpis.Add(new AnswerKpiDto("Lucro Líquido", np.GetDecimal().ToString("C2", new System.Globalization.CultureInfo("pt-BR")), ChartValueFormat.Currency));

            if (result.TryGetProperty("marginPercent", out var mp))
                kpis.Add(new AnswerKpiDto("Margem", $"{mp.GetDecimal():F1}%", ChartValueFormat.Percent));

            return kpis;
        }
        catch
        {
            return null;
        }
    }

    private static string? ResolvePeriodLabel(IReadOnlyList<(string Name, JsonElement Args, JsonElement Result)> toolResults)
    {
        foreach (var (_, args, _) in toolResults)
        {
            if (args.TryGetProperty("from", out var fromProp) && args.TryGetProperty("to", out var toProp))
            {
                return $"{fromProp.GetString()} a {toProp.GetString()}";
            }
        }
        return null;
    }

    private static ChatAnswerDto BuildFallbackAnswer(
        Guid conversationId,
        string message,
        int latencyMs,
        AiUsageDto usage)
    {
        return new ChatAnswerDto(
            conversationId,
            Guid.NewGuid(),
            message,
            new List<AnswerBlockDto> { new(AnswerBlockType.Text, Text: message) },
            new AiMetaDto("Gemini", "gemini-1.5-flash", null, latencyMs, Array.Empty<string>(), IsFallback: true),
            usage);
    }
}

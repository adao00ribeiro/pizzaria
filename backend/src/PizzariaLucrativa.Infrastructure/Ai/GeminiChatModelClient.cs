using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Interfaces;
using PizzariaLucrativa.Application.Options;

namespace PizzariaLucrativa.Infrastructure.Ai;

/// <summary>
/// Implementação de IChatModelClient para a API do Google Gemini (Gemini Developer API).
/// Nunca loga a chave de API e envia sempre via cabeçalho x-goog-api-key.
/// </summary>
public sealed class GeminiChatModelClient : IChatModelClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly HttpClient _httpClient;
    private readonly AiOptions _options;
    private readonly ILogger<GeminiChatModelClient> _logger;

    public GeminiChatModelClient(
        HttpClient httpClient,
        IOptions<AiOptions> options,
        ILogger<GeminiChatModelClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ChatCompletionResult> CompleteAsync(
        ChatCompletionRequest request,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        if (!_options.Enabled)
        {
            return new ChatCompletionResult(
                Text: null,
                ToolCalls: Array.Empty<ChatToolCall>(),
                InputTokens: 0,
                OutputTokens: 0,
                Provider: "Gemini",
                Model: _options.Model,
                LatencyMs: 0,
                IsFallback: true);
        }

        var endpoint = $"/v1beta/models/{_options.Model}:generateContent";
        var requestPayload = BuildGeminiPayload(request);

        const int maxRetries = 2;
        var attempt = 0;

        while (true)
        {
            attempt++;
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.TimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            try
            {
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
                httpRequest.Headers.Add("x-goog-api-key", _options.ApiKey);
                httpRequest.Content = new StringContent(
                    JsonSerializer.Serialize(requestPayload, JsonOptions),
                    Encoding.UTF8,
                    "application/json");

                using var response = await _httpClient.SendAsync(httpRequest, linkedCts.Token);

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync(linkedCts.Token);
                    sw.Stop();
                    return ParseGeminiResponse(responseJson, (int)sw.ElapsedMilliseconds);
                }

                var statusCode = (int)response.StatusCode;
                var errorBody = await response.Content.ReadAsStringAsync(linkedCts.Token);

                // Retry apenas para 429 (TooManyRequests) e 5xx (Server Error)
                var isTransient = statusCode == 429 || (statusCode >= 500 && statusCode < 600);
                if (isTransient && attempt <= maxRetries)
                {
                    var delayMs = attempt * 800;
                    _logger.LogWarning(
                        "Gemini retornou status {StatusCode}. Tentativa {Attempt} de {MaxRetries}. Aguardando {DelayMs}ms...",
                        statusCode, attempt, maxRetries, delayMs);
                    await Task.Delay(delayMs, ct);
                    continue;
                }

                _logger.LogError("Erro do Gemini API ({StatusCode}): {ErrorBody}", statusCode, errorBody);
                sw.Stop();
                return new ChatCompletionResult(
                    Text: null,
                    ToolCalls: Array.Empty<ChatToolCall>(),
                    InputTokens: 0,
                    OutputTokens: 0,
                    Provider: "Gemini",
                    Model: _options.Model,
                    LatencyMs: (int)sw.ElapsedMilliseconds,
                    IsFallback: true);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning("Timeout ao aguardar resposta do Google Gemini ({Timeout}s).", _options.TimeoutSeconds);
                sw.Stop();
                return new ChatCompletionResult(
                    Text: null,
                    ToolCalls: Array.Empty<ChatToolCall>(),
                    InputTokens: 0,
                    OutputTokens: 0,
                    Provider: "Gemini",
                    Model: _options.Model,
                    LatencyMs: (int)sw.ElapsedMilliseconds,
                    IsFallback: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha na comunicação com o Google Gemini.");
                sw.Stop();
                return new ChatCompletionResult(
                    Text: null,
                    ToolCalls: Array.Empty<ChatToolCall>(),
                    InputTokens: 0,
                    OutputTokens: 0,
                    Provider: "Gemini",
                    Model: _options.Model,
                    LatencyMs: (int)sw.ElapsedMilliseconds,
                    IsFallback: true);
            }
        }
    }

    private JsonObject BuildGeminiPayload(ChatCompletionRequest request)
    {
        var root = new JsonObject();

        // 1. systemInstruction
        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            root["systemInstruction"] = new JsonObject
            {
                ["parts"] = new JsonArray
                {
                    new JsonObject { ["text"] = request.SystemPrompt }
                }
            };
        }

        // 2. contents
        var contentsArray = new JsonArray();
        foreach (var message in request.Messages)
        {
            var contentObj = new JsonObject();
            var partsArray = new JsonArray();

            var role = message.Role.ToLowerInvariant();
            if (role == "tool" || role == "function")
            {
                contentObj["role"] = "function";

                var funcResp = new JsonObject
                {
                    ["name"] = message.ToolName ?? "unknown",
                    ["response"] = new JsonObject
                    {
                        ["name"] = message.ToolName ?? "unknown",
                        ["content"] = message.ToolResponse.HasValue
                            ? JsonNode.Parse(message.ToolResponse.Value.GetRawText())
                            : new JsonObject()
                    }
                };

                partsArray.Add(new JsonObject { ["functionResponse"] = funcResp });
            }
            else
            {
                contentObj["role"] = role == "assistant" || role == "model" ? "model" : "user";

                if (!string.IsNullOrEmpty(message.Content))
                {
                    partsArray.Add(new JsonObject { ["text"] = message.Content });
                }

                if (message.ToolCalls != null && message.ToolCalls.Count > 0)
                {
                    foreach (var tc in message.ToolCalls)
                    {
                        var callObj = new JsonObject
                        {
                            ["name"] = tc.Name,
                            ["args"] = JsonNode.Parse(tc.Arguments.GetRawText()) ?? new JsonObject()
                        };
                        partsArray.Add(new JsonObject { ["functionCall"] = callObj });
                    }
                }
            }

            contentObj["parts"] = partsArray;
            contentsArray.Add(contentObj);
        }
        root["contents"] = contentsArray;

        // 3. tools
        if (request.Tools.Count > 0)
        {
            var funcDeclarations = new JsonArray();
            foreach (var t in request.Tools)
            {
                var decl = new JsonObject
                {
                    ["name"] = t.Name,
                    ["description"] = t.Description,
                    ["parameters"] = JsonNode.Parse(t.Parameters.GetRawText())
                };
                funcDeclarations.Add(decl);
            }

            root["tools"] = new JsonArray
            {
                new JsonObject { ["functionDeclarations"] = funcDeclarations }
            };

            root["toolConfig"] = new JsonObject
            {
                ["functionCallingConfig"] = new JsonObject
                {
                    ["mode"] = "AUTO"
                }
            };
        }

        // 4. generationConfig
        root["generationConfig"] = new JsonObject
        {
            ["temperature"] = request.Temperature,
            ["maxOutputTokens"] = request.MaxOutputTokens
        };

        return root;
    }

    private ChatCompletionResult ParseGeminiResponse(string json, int latencyMs)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var inputTokens = 0;
            var outputTokens = 0;
            if (root.TryGetProperty("usageMetadata", out var usage))
            {
                if (usage.TryGetProperty("promptTokenCount", out var ptc))
                    inputTokens = ptc.GetInt32();
                if (usage.TryGetProperty("candidatesTokenCount", out var ctc))
                    outputTokens = ctc.GetInt32();
            }

            if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            {
                return new ChatCompletionResult(
                    Text: null,
                    ToolCalls: Array.Empty<ChatToolCall>(),
                    InputTokens: inputTokens,
                    OutputTokens: outputTokens,
                    Provider: "Gemini",
                    Model: _options.Model,
                    LatencyMs: latencyMs,
                    IsFallback: false);
            }

            var firstCandidate = candidates[0];
            if (!firstCandidate.TryGetProperty("content", out var content) ||
                !content.TryGetProperty("parts", out var parts))
            {
                return new ChatCompletionResult(
                    Text: null,
                    ToolCalls: Array.Empty<ChatToolCall>(),
                    InputTokens: inputTokens,
                    OutputTokens: outputTokens,
                    Provider: "Gemini",
                    Model: _options.Model,
                    LatencyMs: latencyMs,
                    IsFallback: false);
            }

            var textBuilder = new StringBuilder();
            var toolCalls = new List<ChatToolCall>();

            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var textElem))
                {
                    textBuilder.Append(textElem.GetString());
                }

                if (part.TryGetProperty("functionCall", out var fc))
                {
                    var name = fc.GetProperty("name").GetString() ?? string.Empty;
                    var args = fc.GetProperty("args").Clone();
                    toolCalls.Add(new ChatToolCall(Guid.NewGuid().ToString("N"), name, args));
                }
            }

            var fullText = textBuilder.Length > 0 ? textBuilder.ToString() : null;

            return new ChatCompletionResult(
                Text: fullText,
                ToolCalls: toolCalls,
                InputTokens: inputTokens,
                OutputTokens: outputTokens,
                Provider: "Gemini",
                Model: _options.Model,
                LatencyMs: latencyMs,
                IsFallback: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao deserializar resposta do Gemini.");
            return new ChatCompletionResult(
                Text: null,
                ToolCalls: Array.Empty<ChatToolCall>(),
                InputTokens: 0,
                OutputTokens: 0,
                Provider: "Gemini",
                Model: _options.Model,
                LatencyMs: latencyMs,
                IsFallback: true);
        }
    }
}

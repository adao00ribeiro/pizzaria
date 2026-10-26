using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using PizzariaLucrativa.Api.Controllers.Shared;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Interfaces;
using PizzariaLucrativa.Application.Options;

namespace PizzariaLucrativa.Api.Controllers;

/// <summary>
/// Endpoints do assistente de IA da Pizzaria Lucrativa (IA3-12).
/// Acesso restrito ao proprietário (OwnerOnly).
/// </summary>
[Authorize(Policy = "OwnerOnly")]
public sealed class AiController : ApiControllerBase
{
    private readonly IAiChatService _chatService;
    private readonly IAiConversationStore _conversationStore;
    private readonly IAiUsageService _usageService;
    private readonly AiOptions _options;

    public AiController(
        IAiChatService chatService,
        IAiConversationStore conversationStore,
        IAiUsageService usageService,
        IOptions<AiOptions> options)
    {
        _chatService = chatService;
        _conversationStore = conversationStore;
        _usageService = usageService;
        _options = options.Value;
    }

    /// <summary>Envia pergunta ao assistente de IA com suporte a function calling e histórico.</summary>
    [HttpPost("chat")]
    [EnableRateLimiting("ai-chat")]
    [ProducesResponseType(typeof(ChatAnswerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ChatAnswerDto>> Chat(
        [FromBody] ChatRequestDto request,
        CancellationToken ct = default)
    {
        var response = await _chatService.AskAsync(request, ct);
        return Ok(response);
    }

    /// <summary>Lista sugestões de perguntas pré-configuradas para o proprietário.</summary>
    [HttpGet("suggestions")]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<string>> Suggestions()
    {
        var suggestions = new[]
        {
            "Quanto lucrei no mês passado?",
            "Quais regiões da cidade tiveram mais pedidos?",
            "Qual produto deu mais lucro nos últimos 30 dias?",
            "Compare o faturamento deste mês com o anterior.",
            "Quais insumos estão com estoque crítico ou zerado?"
        };

        return Ok(suggestions);
    }

    /// <summary>Informa o status do subsistema de IA sem expor chaves ou segredos (IA3-11).</summary>
    [HttpGet("health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult GetHealth()
    {
        return Ok(new
        {
            enabled = _options.Enabled,
            provider = _options.Provider,
            model = _options.Model,
            historyEnabled = _options.History.Enabled,
            budgetConfigured = _options.Budget.MonthlyUsdPerTenant > 0,
            apiKeyConfigured = !string.IsNullOrWhiteSpace(_options.ApiKey)
        });
    }

    /// <summary>Lista as conversas anteriores do proprietário.</summary>
    [HttpGet("conversations")]
    [ProducesResponseType(typeof(IReadOnlyList<ConversationSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ConversationSummaryDto>>> ListConversations(
        [FromQuery] bool includeArchived = false,
        CancellationToken ct = default)
    {
        var list = await _conversationStore.ListAsync(includeArchived, ct);
        return Ok(list);
    }

    /// <summary>Recupera o histórico completo de uma conversa.</summary>
    [HttpGet("conversations/{id:guid}")]
    [ProducesResponseType(typeof(ConversationDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConversationDetailDto>> GetConversation(
        Guid id,
        CancellationToken ct = default)
    {
        var conversation = await _conversationStore.GetAsync(id, ct);
        return Ok(conversation);
    }

    /// <summary>Exclui uma conversa e suas mensagens.</summary>
    [HttpDelete("conversations/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteConversation(
        Guid id,
        CancellationToken ct = default)
    {
        await _conversationStore.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>Consulta o consumo de tokens e status do orçamento mensal do tenant.</summary>
    [HttpGet("usage")]
    [ProducesResponseType(typeof(AiUsageDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AiUsageDto>> GetUsage(CancellationToken ct = default)
    {
        var usage = await _usageService.GetUsageAsync(ct);
        return Ok(usage);
    }
}

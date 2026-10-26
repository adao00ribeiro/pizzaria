using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Exceptions;
using PizzariaLucrativa.Application.Interfaces;
using PizzariaLucrativa.Application.Options;
using PizzariaLucrativa.Domain.Entities;
using PizzariaLucrativa.Infrastructure.Persistence;

namespace PizzariaLucrativa.Infrastructure.Ai;

/// <summary>
/// Persistência do histórico do assistente no banco de dados (IA3-08).
/// O escopo é estritamente isolado pelo tenant do usuário logado.
/// </summary>
public sealed class AiConversationStore : IAiConversationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly AppDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly AiOptions _options;

    public AiConversationStore(
        AppDbContext context,
        ICurrentUser currentUser,
        IOptions<AiOptions> options)
    {
        _context = context;
        _currentUser = currentUser;
        _options = options.Value;
    }

    public async Task<ConversationDetailDto> GetOrCreateAsync(
        Guid? conversationId,
        string title,
        CancellationToken ct = default)
    {
        if (!_options.History.Enabled)
        {
            var fallbackId = conversationId ?? Guid.NewGuid();
            return new ConversationDetailDto(
                fallbackId,
                title,
                DateTime.UtcNow,
                DateTime.UtcNow,
                false,
                Array.Empty<ConversationMessageDto>());
        }

        if (conversationId.HasValue)
        {
            var existing = await _context.AiConversations
                .Include(c => c.Messages)
                .FirstOrDefaultAsync(c => c.Id == conversationId.Value, ct);

            if (existing is null)
            {
                throw new NotFoundException("Conversa não encontrada.");
            }

            if (!string.IsNullOrWhiteSpace(title) &&
                (string.IsNullOrWhiteSpace(existing.Title) || existing.Title == "Nova conversa"))
            {
                existing.Title = Truncate(title, 160);
                existing.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(ct);
            }

            return MapToDetail(existing);
        }

        var newTitle = string.IsNullOrWhiteSpace(title) ? "Nova conversa" : Truncate(title, 160);
        var conversation = new AiConversation
        {
            Id = Guid.NewGuid(),
            Title = newTitle,
            UserId = _currentUser.UserId ?? string.Empty,
            IsArchived = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.AiConversations.Add(conversation);
        await _context.SaveChangesAsync(ct);

        return new ConversationDetailDto(
            conversation.Id,
            conversation.Title,
            conversation.CreatedAt,
            conversation.UpdatedAt ?? conversation.CreatedAt,
            conversation.IsArchived,
            Array.Empty<ConversationMessageDto>());
    }

    public async Task<IReadOnlyList<ConversationSummaryDto>> ListAsync(
        bool includeArchived = false,
        CancellationToken ct = default)
    {
        if (!_options.History.Enabled)
        {
            return Array.Empty<ConversationSummaryDto>();
        }

        var query = _context.AiConversations.AsNoTracking();

        if (!includeArchived)
        {
            query = query.Where(c => !c.IsArchived);
        }

        var list = await query
            .OrderByDescending(c => c.UpdatedAt)
            .Select(c => new ConversationSummaryDto(
                c.Id,
                c.Title,
                c.CreatedAt,
                c.UpdatedAt ?? c.CreatedAt,
                c.Messages.Count,
                c.IsArchived))
            .ToListAsync(ct);

        return list;
    }

    public async Task<ConversationDetailDto> GetAsync(
        Guid conversationId,
        CancellationToken ct = default)
    {
        if (!_options.History.Enabled)
        {
            throw new NotFoundException("Histórico de IA desabilitado.");
        }

        var conversation = await _context.AiConversations
            .AsNoTracking()
            .Include(c => c.Messages)
            .FirstOrDefaultAsync(c => c.Id == conversationId, ct);

        if (conversation is null)
        {
            throw new NotFoundException("Conversa não encontrada.");
        }

        return MapToDetail(conversation);
    }

    public async Task<ConversationMessageDto> AppendMessageAsync(
        Guid conversationId,
        ChatMessageDto message,
        CancellationToken ct = default)
    {
        if (!_options.History.Enabled)
        {
            return new ConversationMessageDto(
                Guid.NewGuid(),
                message.Role,
                message.Content,
                DateTime.UtcNow,
                message.ToolsUsed ?? Array.Empty<string>(),
                message.EstimatedCostUsd,
                message.Blocks);
        }

        var conversation = await _context.AiConversations
            .FirstOrDefaultAsync(c => c.Id == conversationId, ct);

        if (conversation is null)
        {
            throw new NotFoundException("Conversa não encontrada.");
        }

        var aiMessage = new AiMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            Role = message.Role,
            Content = Truncate(message.Content, 8_000),
            BlocksJson = message.Blocks != null && message.Blocks.Count > 0
                ? JsonSerializer.Serialize(message.Blocks, JsonOptions)
                : null,
            ToolsUsed = message.ToolsUsed != null && message.ToolsUsed.Count > 0
                ? Truncate(string.Join(",", message.ToolsUsed), 500)
                : null,
            InputTokens = message.InputTokens,
            OutputTokens = message.OutputTokens,
            EstimatedCostUsd = message.EstimatedCostUsd,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        conversation.UpdatedAt = DateTime.UtcNow;

        _context.AiMessages.Add(aiMessage);
        await _context.SaveChangesAsync(ct);

        var tools = !string.IsNullOrEmpty(aiMessage.ToolsUsed)
            ? aiMessage.ToolsUsed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : Array.Empty<string>();

        return new ConversationMessageDto(
            aiMessage.Id,
            aiMessage.Role,
            aiMessage.Content,
            aiMessage.CreatedAt,
            tools,
            aiMessage.EstimatedCostUsd,
            message.Blocks);
    }

    public async Task DeleteAsync(Guid conversationId, CancellationToken ct = default)
    {
        if (!_options.History.Enabled)
        {
            return;
        }

        var conversation = await _context.AiConversations
            .FirstOrDefaultAsync(c => c.Id == conversationId, ct);

        if (conversation is null)
        {
            throw new NotFoundException("Conversa não encontrada.");
        }

        _context.AiConversations.Remove(conversation);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<int> PurgeExpiredAsync(DateTime olderThanUtc, CancellationToken ct = default)
    {
        var expired = await _context.AiConversations
            .Where(c => c.UpdatedAt < olderThanUtc)
            .ToListAsync(ct);

        if (expired.Count == 0)
        {
            return 0;
        }

        _context.AiConversations.RemoveRange(expired);
        return await _context.SaveChangesAsync(ct);
    }

    private static ConversationDetailDto MapToDetail(AiConversation conversation)
    {
        var messages = conversation.Messages
            .OrderBy(m => m.CreatedAt)
            .Select(m =>
            {
                var tools = !string.IsNullOrEmpty(m.ToolsUsed)
                    ? m.ToolsUsed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    : Array.Empty<string>();

                IReadOnlyList<AnswerBlockDto>? blocks = null;
                if (!string.IsNullOrEmpty(m.BlocksJson))
                {
                    try
                    {
                        blocks = JsonSerializer.Deserialize<List<AnswerBlockDto>>(m.BlocksJson, JsonOptions);
                    }
                    catch
                    {
                        // Resiliente caso haja JSON legado/inválido
                    }
                }

                return new ConversationMessageDto(
                    m.Id,
                    m.Role,
                    m.Content,
                    m.CreatedAt,
                    tools,
                    m.EstimatedCostUsd,
                    blocks);
            })
            .ToList();

        return new ConversationDetailDto(
            conversation.Id,
            conversation.Title,
            conversation.CreatedAt,
            conversation.UpdatedAt ?? conversation.CreatedAt,
            conversation.IsArchived,
            messages);
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }
}

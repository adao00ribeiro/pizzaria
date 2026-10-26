using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Interfaces;
using PizzariaLucrativa.Application.Options;
using PizzariaLucrativa.Domain.Entities;

namespace PizzariaLucrativa.Application.Services;

/// <summary>
/// Contabilização de tokens, latência, custos e controle de orçamento mensal por tenant (IA3-10).
/// Registrado em AiUsageRecord mesmo se o histórico de conversas estiver desativado.
/// </summary>
public sealed class AiUsageService : IAiUsageService
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly AiOptions _options;
    private readonly ILogger<AiUsageService> _logger;

    public AiUsageService(
        IAppDbContext context,
        ICurrentUser currentUser,
        IOptions<AiOptions> options,
        ILogger<AiUsageService> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AiUsageDto> GetUsageAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var monthRecords = await _context.Set<AiUsageRecord>()
            .Where(r => r.CreatedAt >= startOfMonth)
            .ToListAsync(ct);

        var monthlyInputTokens = monthRecords.Sum(r => r.InputTokens);
        var monthlyOutputTokens = monthRecords.Sum(r => r.OutputTokens);
        var monthlyCost = monthRecords.Sum(r => r.EstimatedCostUsd);

        var latestRecord = monthRecords.OrderByDescending(r => r.CreatedAt).FirstOrDefault();

        var budgetLimit = _options.Budget.MonthlyUsdPerTenant;
        var budgetPercentage = budgetLimit > 0 ? (monthlyCost / budgetLimit) * 100m : 0m;
        var budgetExceeded = _options.Budget.Enforce && monthlyCost >= budgetLimit;

        return new AiUsageDto(
            InputTokens: latestRecord?.InputTokens ?? 0,
            OutputTokens: latestRecord?.OutputTokens ?? 0,
            EstimatedCostUsd: latestRecord?.EstimatedCostUsd ?? 0m,
            MonthlyInputTokens: monthlyInputTokens,
            MonthlyOutputTokens: monthlyOutputTokens,
            MonthlyCostUsd: Math.Round(monthlyCost, 4),
            MonthlyBudgetUsd: budgetLimit,
            BudgetUsedPercentage: Math.Round(budgetPercentage, 2),
            BudgetExceeded: budgetExceeded,
            IsFallback: latestRecord?.IsFallback ?? false);
    }

    public async Task<bool> CanSpendAsync(decimal estimatedCostUsd, CancellationToken ct = default)
    {
        if (!_options.Budget.Enforce)
        {
            return true;
        }

        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var monthlyCost = await _context.Set<AiUsageRecord>()
            .Where(r => r.CreatedAt >= startOfMonth)
            .SumAsync(r => r.EstimatedCostUsd, ct);

        var allowed = (monthlyCost + estimatedCostUsd) <= _options.Budget.MonthlyUsdPerTenant;
        if (!allowed)
        {
            _logger.LogWarning(
                "Orçamento mensal de IA atingido (Gasto={MonthlyCost:F4}, Tentativa={EstimatedCost:F4}, Teto={Limit:F4}). Chamada bloqueada.",
                monthlyCost, estimatedCostUsd, _options.Budget.MonthlyUsdPerTenant);
        }

        return allowed;
    }

    public async Task RecordAsync(AiUsageEntryDto entry, CancellationToken ct = default)
    {
        Guid? validConversationId = null;
        if (entry.ConversationId.HasValue && _options.History.Enabled)
        {
            var exists = await _context.Set<AiConversation>()
                .AnyAsync(c => c.Id == entry.ConversationId.Value, ct);

            if (exists)
            {
                validConversationId = entry.ConversationId.Value;
            }
        }

        var record = new AiUsageRecord
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId ?? string.Empty,
            ConversationId = validConversationId,
            Provider = entry.Provider,
            Model = entry.Model,
            InputTokens = entry.InputTokens,
            OutputTokens = entry.OutputTokens,
            EstimatedCostUsd = entry.EstimatedCostUsd,
            LatencyMs = entry.LatencyMs,
            IsFallback = entry.IsFallback,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Add(record);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Consumo de IA registrado: Model={Model}, Tokens={In}/{Out}, Custo=${Cost:F5}, Latência={Latency}ms, Fallback={Fallback}",
            entry.Model, entry.InputTokens, entry.OutputTokens, entry.EstimatedCostUsd, entry.LatencyMs, entry.IsFallback);
    }
}

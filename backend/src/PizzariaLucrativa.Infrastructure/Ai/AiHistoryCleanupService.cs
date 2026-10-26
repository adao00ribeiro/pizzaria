using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PizzariaLucrativa.Application.Options;
using PizzariaLucrativa.Infrastructure.Persistence;

namespace PizzariaLucrativa.Infrastructure.Ai;

/// <summary>
/// Serviço em segundo plano responsável pela retenção e limpeza automática
/// de histórico de conversas da IA (IA3-09).
/// Quando RetentionDays == 0, a limpeza automática fica desativada.
/// </summary>
public sealed class AiHistoryCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptionsMonitor<AiOptions> _optionsMonitor;
    private readonly ILogger<AiHistoryCleanupService> _logger;
    private readonly TimeSpan _checkInterval;

    public AiHistoryCleanupService(
        IServiceProvider serviceProvider,
        IOptionsMonitor<AiOptions> optionsMonitor,
        ILogger<AiHistoryCleanupService> logger,
        TimeSpan? checkInterval = null)
    {
        _serviceProvider = serviceProvider;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
        _checkInterval = checkInterval ?? TimeSpan.FromHours(12);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AiHistoryCleanupService iniciado.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao executar limpeza periódica do histórico de IA.");
            }

            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public async Task<int> RunCleanupAsync(CancellationToken ct = default)
    {
        var options = _optionsMonitor.CurrentValue;

        if (!options.History.Enabled || options.History.RetentionDays <= 0)
        {
            _logger.LogDebug(
                "Limpeza automática de histórico de IA ignorada (Enabled={Enabled}, RetentionDays={RetentionDays}).",
                options.History.Enabled, options.History.RetentionDays);
            return 0;
        }

        var cutoff = DateTime.UtcNow.AddDays(-options.History.RetentionDays);
        _logger.LogInformation("Executando limpeza de conversas de IA anteriores a {CutoffUtc}...", cutoff);

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var expiredConversations = await context.AiConversations
            .IgnoreQueryFilters()
            .Where(c => c.UpdatedAt < cutoff)
            .ToListAsync(ct);

        if (expiredConversations.Count == 0)
        {
            return 0;
        }

        context.AiConversations.RemoveRange(expiredConversations);
        var removed = await context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Limpeza de histórico de IA concluída: {Count} conversas removidas.",
            expiredConversations.Count);

        return expiredConversations.Count;
    }
}

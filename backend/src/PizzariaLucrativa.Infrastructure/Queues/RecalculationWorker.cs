using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PizzariaLucrativa.Application.Interfaces;

namespace PizzariaLucrativa.Infrastructure.Queues;

/// <summary>
/// consome a fila de recálculo em cadeia e executa IRecalculationService
/// (preparos → fichas → combos → alertas) em scope próprio (docs/04 §5).
/// </summary>
public sealed class RecalculationWorker : BackgroundService
{
    private readonly RecalculationQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RecalculationWorker> _logger;

    public RecalculationWorker(
        RecalculationQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<RecalculationWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var tenantId in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            _queue.MarkDequeued(tenantId);

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var recalculation = scope.ServiceProvider.GetRequiredService<IRecalculationService>();
                await recalculation.RecalculateTenantAsync(tenantId, stoppingToken);
                _logger.LogInformation("Recálculo em cadeia do tenant {TenantId} concluído.", tenantId);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Não derruba o worker: o próximo enqueue tenta de novo.
                _logger.LogError(ex, "Falha ao recalcular o tenant {TenantId}.", tenantId);
            }
        }
    }
}

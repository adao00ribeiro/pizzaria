using System.Threading.Channels;
using PizzariaLucrativa.Application.Interfaces;

namespace PizzariaLucrativa.Infrastructure.Queues;

/// <summary>
/// Fila em memória (Channel&lt;Guid&gt;) de recálculos em cadeia por tenant (docs/04 §5).
/// Deduplicação: se o tenant já está pendente, novo enqueue é ignorado — o recálculo
/// completo é idempotente (NF-R02), então processar uma vez cobre todas as mudanças
/// que empilharam enquanto a fila esperava.
/// </summary>
public sealed class RecalculationQueue : IRecalculationQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateBounded<Guid>(
        new BoundedChannelOptions(1000)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });

    private readonly HashSet<Guid> _pending = new();
    private readonly object _gate = new();

    internal ChannelReader<Guid> Reader => _channel.Reader;

    public ValueTask EnqueueAsync(Guid tenantId, CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (!_pending.Add(tenantId))
                return ValueTask.CompletedTask; // já enfileirado — dedupe
        }

        if (!_channel.Writer.TryWrite(tenantId))
        {
            lock (_gate)
            {
                _pending.Remove(tenantId);
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>Remove o tenant da lista de pendentes (chamado pelo worker ao iniciar o processamento).</summary>
    internal void MarkDequeued(Guid tenantId)
    {
        lock (_gate)
        {
            _pending.Remove(tenantId);
        }
    }
}

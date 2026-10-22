namespace PizzariaLucrativa.Application.Interfaces;

/// <summary>Fila em memória de recálculos em cadeia (docs/04 §5) — Channel&lt;Guid&gt; na Infrastructure.</summary>
public interface IRecalculationQueue
{
    /// <summary>Enfileira o tenant para recálculo completo (idempotente, NF-R02).</summary>
    ValueTask EnqueueAsync(Guid tenantId, CancellationToken ct = default);
}

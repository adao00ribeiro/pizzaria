using PizzariaLucrativa.Application.DTOs;

namespace PizzariaLucrativa.Application.Interfaces;

public interface IKitchenService
{
    /// <summary>Fila da produção (docs/10, Sprint 3): projeção enxuta + contagem por status.</summary>
    Task<KitchenQueueDto> QueueAsync(CancellationToken ct = default);
}

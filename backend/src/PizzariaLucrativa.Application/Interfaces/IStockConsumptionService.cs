using PizzariaLucrativa.Domain.Entities;

namespace PizzariaLucrativa.Application.Interfaces;

public interface IStockConsumptionService
{
    /// <summary>
    /// Baixa o estoque dos ingredientes consumidos pelo pedido (docs/10, Sprint 2).
    /// Idempotente: a segunda chamada para 'Pronto' não baixa de novo.
    /// Devolve true quando gravou movimentos nesta chamada.
    /// </summary>
    Task<bool> ConsumeForOrderAsync(Order order, CancellationToken ct = default);

    /// <summary>
    /// Estorna a baixa do pedido (cancelamento após 'Pronto') com movimentos de
    /// entrada. Idempotente. Devolve true quando estornou nesta chamada.
    /// </summary>
    Task<bool> ReverseForOrderAsync(Order order, CancellationToken ct = default);
}

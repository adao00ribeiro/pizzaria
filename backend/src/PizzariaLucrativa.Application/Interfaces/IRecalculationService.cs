namespace PizzariaLucrativa.Application.Interfaces;

public interface IRecalculationService
{
    /// <summary>
    /// Recálculo em cadeia de um tenant (docs/06 §11): preparos → fichas técnicas →
    /// combos → alertas de margem → alertas de estoque. Idempotente e transacional (NF-R02/R03).
    /// </summary>
    Task RecalculateTenantAsync(Guid tenantId, CancellationToken ct = default);
}

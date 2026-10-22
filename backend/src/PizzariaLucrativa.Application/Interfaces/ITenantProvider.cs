namespace PizzariaLucrativa.Application.Interfaces;

/// <summary>
/// Tenant corrente, resolvido do claim JWT (NF-S03/S04) — nunca do body.
/// Implementação usa AsyncLocal para também funcionar em BackgroundService.
/// </summary>
public interface ITenantProvider
{
    /// <summary>Tenant do request/fluxo atual; null quando anônimo.</summary>
    Guid? TenantId { get; }

    /// <summary>Tenant corrente ou exceção se não houver contexto autenticado.</summary>
    Guid GetTenantId();

    /// <summary>Define o tenant do fluxo assíncrono atual (middleware ou worker).</summary>
    void SetTenant(Guid tenantId);

    /// <summary>Limpa o contexto (fim do request). </summary>
    void Clear();
}

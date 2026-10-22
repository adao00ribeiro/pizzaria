using PizzariaLucrativa.Application.Exceptions;
using PizzariaLucrativa.Application.Interfaces;

namespace PizzariaLucrativa.Infrastructure.Identity;

/// <summary>
/// Resolvido do claim JWT pelo middleware da API (ou pelo worker de recálculo).
/// Usa AsyncLocal para o valor fluir por todo o fluxo assíncrono do request e
/// para que o BackgroundService possa definir o tenant do processamento (NF-S03).
/// </summary>
public sealed class TenantProvider : ITenantProvider
{
    private readonly AsyncLocal<Guid?> _tenantId = new();

    public Guid? TenantId => _tenantId.Value;

    public Guid GetTenantId()
    {
        return _tenantId.Value
               ?? throw new UnauthorizedException("Contexto de tenant não autenticado.");
    }

    public void SetTenant(Guid tenantId) => _tenantId.Value = tenantId;

    public void Clear() => _tenantId.Value = null;
}

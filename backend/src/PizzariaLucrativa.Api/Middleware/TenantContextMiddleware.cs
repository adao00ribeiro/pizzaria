using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;
using PizzariaLucrativa.Application.Exceptions;
using PizzariaLucrativa.Application.Interfaces;

namespace PizzariaLucrativa.Api.Middleware;

/// <summary>
/// Resolve o tenant logo após a autenticação (NF-S03/S04):
///  1) bypass para Administrador do Sistema (claim system_admin == "true");
///  2) claim JWT <c>tenant_id</c> (rotas autenticadas) com verificação de IsActive (403 se inativo);
///  3) segmento <c>{slug}</c> da rota pública <c>/api/v1/public/{slug}/...</c> (404 se inativo/inexistente).
/// Precisa ser middleware (e não evento do JwtBearer): o valor do AsyncLocal gravado
/// dentro do handler de autenticação não flui de volta para o resto do pipeline.
/// </summary>
public sealed class TenantContextMiddleware
{
    private const string PublicPrefix = "/api/v1/public/";

    private readonly RequestDelegate _next;

    public TenantContextMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(
        HttpContext context,
        ITenantProvider tenantProvider,
        IAppDbContext db,
        IMemoryCache cache)
    {
        // Administrador do sistema não possui tenant e opera sobre o plano global
        if (context.User.HasClaim("system_admin", "true"))
        {
            await _next(context);
            return;
        }

        // Rotas públicas (/api/v1/public/{slug}/...) resolvem o tenant exclusivamente pelo slug
        // da URL, mesmo que o cliente envie um JWT de outro tenant no header (docs/10 DoD).
        var slug = ExtractPublicSlug(context.Request.Path.Value);
        if (slug is not null)
        {
            var resolved = await ResolveSlugAsync(db, cache, slug, context.RequestAborted);
            if (resolved == Guid.Empty)
                throw new NotFoundException("Pizzaria não encontrada ou estabelecimento temporariamente indisponível.");

            tenantProvider.SetTenant(resolved);
        }
        else
        {
            var tenantClaim = context.User.FindFirstValue("tenant_id");
            if (Guid.TryParse(tenantClaim, out var tenantId))
            {
                var isActive = await cache.GetOrCreateAsync($"tenant_active_{tenantId}", async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
                    var t = await db.FirstOrDefaultAsync(
                        db.Set<Domain.Entities.Tenant>().Where(x => x.Id == tenantId),
                        context.RequestAborted);
                    return t is not null && t.IsActive;
                });

                if (!isActive)
                    throw new ForbiddenException("Acesso suspenso pelo administrador do sistema.");

                tenantProvider.SetTenant(tenantId);
            }
        }

        try
        {
            await _next(context);
        }
        finally
        {
            tenantProvider.Clear();
        }
    }

    /// <summary>Extrai o slug de /api/v1/public/{slug}/... (null se não for rota pública).</summary>
    private static string? ExtractPublicSlug(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)
            || !path.StartsWith(PublicPrefix, StringComparison.OrdinalIgnoreCase))
            return null;

        var remainder = path[PublicPrefix.Length..];
        var slash = remainder.IndexOf('/');
        var candidate = slash >= 0 ? remainder[..slash] : remainder;
        return string.IsNullOrWhiteSpace(candidate) ? null : candidate.Trim().ToLowerInvariant();
    }

    private static async Task<Guid> ResolveSlugAsync(IAppDbContext db, IMemoryCache cache, string slug, CancellationToken ct)
    {
        return await cache.GetOrCreateAsync($"tenant_slug_{slug}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
            var tenant = await db.FirstOrDefaultAsync(
                db.Set<Domain.Entities.Tenant>().Where(t => t.Slug == slug), ct);
            if (tenant is null || !tenant.IsActive)
                return Guid.Empty;
            return tenant.Id;
        });
    }
}

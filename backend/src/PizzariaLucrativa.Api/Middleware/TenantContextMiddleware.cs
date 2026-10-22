using System.Security.Claims;
using PizzariaLucrativa.Application.Exceptions;
using PizzariaLucrativa.Application.Interfaces;

namespace PizzariaLucrativa.Api.Middleware;

/// <summary>
/// Resolve o tenant logo após a autenticação (NF-S03/S04):
///  1) claim JWT <c>tenant_id</c> (rotas autenticadas);
///  2) ausindo o claim, o segmento <c>{slug}</c> da rota pública
///     <c>/api/v1/public/{slug}/...</c> — validado contra o banco (404 se inválido).
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
        IAppDbContext db)
    {
        // Rotas públicas (/api/v1/public/{slug}/...) resolvem o tenant exclusivamente pelo slug
        // da URL, mesmo que o cliente envie um JWT de outro tenant no header (docs/10 DoD).
        var slug = ExtractPublicSlug(context.Request.Path.Value);
        if (slug is not null)
        {
            var resolved = await ResolveSlugAsync(db, slug, context.RequestAborted);
            if (resolved == Guid.Empty)
                throw new NotFoundException("Pizzaria não encontrada.");

            tenantProvider.SetTenant(resolved);
        }
        else
        {
            var tenantClaim = context.User.FindFirstValue("tenant_id");
            if (Guid.TryParse(tenantClaim, out var tenantId))
                tenantProvider.SetTenant(tenantId);
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

    private static async Task<Guid> ResolveSlugAsync(IAppDbContext db, string slug, CancellationToken ct)
    {
        var tenant = await db.FirstOrDefaultAsync(
            db.Set<Domain.Entities.Tenant>().Where(t => t.Slug == slug), ct);
        return tenant?.Id ?? Guid.Empty;
    }
}

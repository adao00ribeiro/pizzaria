using Microsoft.EntityFrameworkCore;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Exceptions;
using PizzariaLucrativa.Application.Interfaces;
using PizzariaLucrativa.Domain.Entities;
using PizzariaLucrativa.Domain.Services;

namespace PizzariaLucrativa.Application.Services;

/// <summary>Dados da pizzaria (tenant) — docs/07 §13 (GET/PUT /tenant).</summary>
public sealed class TenantService : ITenantService
{
    private readonly IAppDbContext _db;
    private readonly ITenantProvider _tenant;

    public TenantService(IAppDbContext db, ITenantProvider tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<TenantDto> GetAsync(CancellationToken ct = default)
    {
        var tenant = await FindAsync(ct);
        return new TenantDto(tenant.Id, tenant.Name, tenant.Slug, tenant.CreatedAt);
    }

    public async Task<TenantDto> UpdateAsync(TenantUpdateInput input, CancellationToken ct = default)
    {
        var tenant = await FindAsync(ct);
        var changed = false;

        if (input.Name is not null)
        {
            var name = input.Name.Trim();
            if (string.IsNullOrWhiteSpace(name))
                throw new ValidationException("name", "Informe o nome da pizzaria.");
            if (name.Length > 120)
                throw new ValidationException("name", "Nome pode ter no máximo 120 caracteres.");
            if (!string.Equals(name, tenant.Name, StringComparison.Ordinal))
            {
                tenant.Name = name;
                changed = true;
            }
        }

        if (input.Slug is not null)
        {
            var slugInput = input.Slug.Trim();
            if (string.IsNullOrWhiteSpace(slugInput))
                throw new ValidationException("slug", "Informe um slug válido.");
            if (!SlugGenerator.IsValid(slugInput))
                throw new ValidationException(
                    "slug",
                    "Slug deve ter 3–40 caracteres (minúsculas, números e hífens, sem hífen nas pontas).");

            var normalizedNew = slugInput.ToLowerInvariant();
            var normalizedCurrent = tenant.Slug?.ToLowerInvariant();
            if (!string.Equals(normalizedNew, normalizedCurrent, StringComparison.Ordinal))
            {
                var slugTaken = await _db.Set<Tenant>()
                    .AnyAsync(
                        t => t.Id != tenant.Id
                             && t.Slug != null
                             && t.Slug.ToLower() == normalizedNew,
                        ct);
                if (slugTaken)
                    throw new ConflictException("Slug já está em uso por outra pizzaria.");

                tenant.Slug = normalizedNew;
                changed = true;
            }
        }

        if (changed)
        {
            tenant.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        return new TenantDto(tenant.Id, tenant.Name, tenant.Slug, tenant.CreatedAt);
    }

    private async Task<Tenant> FindAsync(CancellationToken ct)
    {
        var tenantId = _tenant.GetTenantId();
        var tenant = await _db.FirstOrDefaultAsync(
            _db.Set<Tenant>().Where(t => t.Id == tenantId), ct);
        return tenant ?? throw new NotFoundException("Pizzaria não encontrada.");
    }
}

using Microsoft.EntityFrameworkCore;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Exceptions;
using PizzariaLucrativa.Application.Interfaces;
using PizzariaLucrativa.Domain.Entities;

namespace PizzariaLucrativa.Application.Services;

/// <summary>
/// CRUD do catálogo de bairros/regiões do tenant.
/// </summary>
public sealed class NeighborhoodService : INeighborhoodService
{
    private readonly IAppDbContext _db;
    private readonly ITenantProvider _tenant;

    public NeighborhoodService(IAppDbContext db, ITenantProvider tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<NeighborhoodDto>> ListAsync(bool? activeOnly = null, CancellationToken ct = default)
    {
        var query = _db.Set<Neighborhood>().AsQueryable();
        if (activeOnly == true)
            query = query.Where(n => n.IsActive);

        var items = await _db.ToListAsync(query.OrderBy(n => n.Name), ct);
        return items.Select(Map).ToList();
    }

    public async Task<NeighborhoodDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var neighborhood = await _db.FirstOrDefaultAsync(
                _db.Set<Neighborhood>().Where(n => n.Id == id), ct)
            ?? throw new NotFoundException("Bairro não encontrado.");

        return Map(neighborhood);
    }

    public async Task<NeighborhoodDto> CreateAsync(NeighborhoodInputDto input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
            throw new ValidationException("name", "Informe o nome do bairro.");

        var normalizedName = input.Name.Trim();
        var tenantId = _tenant.GetTenantId();

        var exists = await _db.Set<Neighborhood>()
            .AnyAsync(n => n.TenantId == tenantId && n.Name.ToLower() == normalizedName.ToLower(), ct);

        if (exists)
            throw new ConflictException("Já existe um bairro com este nome.");

        var neighborhood = new Neighborhood
        {
            TenantId = tenantId,
            Name = normalizedName,
            Region = input.Region,
            IsActive = input.IsActive
        };

        _db.Add(neighborhood);
        await _db.SaveChangesAsync(ct);
        return Map(neighborhood);
    }

    public async Task<NeighborhoodDto> UpdateAsync(Guid id, NeighborhoodInputDto input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
            throw new ValidationException("name", "Informe o nome do bairro.");

        var neighborhood = await _db.FirstOrDefaultAsync(
                _db.Set<Neighborhood>().Where(n => n.Id == id), ct)
            ?? throw new NotFoundException("Bairro não encontrado.");

        var normalizedName = input.Name.Trim();
        var tenantId = _tenant.GetTenantId();

        var exists = await _db.Set<Neighborhood>()
            .AnyAsync(n => n.TenantId == tenantId && n.Id != id && n.Name.ToLower() == normalizedName.ToLower(), ct);

        if (exists)
            throw new ConflictException("Já existe um bairro com este nome.");

        neighborhood.Name = normalizedName;
        neighborhood.Region = input.Region;
        neighborhood.IsActive = input.IsActive;
        neighborhood.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Map(neighborhood);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var neighborhood = await _db.FirstOrDefaultAsync(
                _db.Set<Neighborhood>().Where(n => n.Id == id), ct)
            ?? throw new NotFoundException("Bairro não encontrado.");

        // Verifica se há pedidos em uso
        var inUse = await _db.Set<Order>().AnyAsync(o => o.NeighborhoodId == id, ct);
        if (inUse)
        {
            // Desativa ao invés de excluir conforme especificado
            neighborhood.IsActive = false;
            neighborhood.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return;
        }

        _db.Remove(neighborhood);
        await _db.SaveChangesAsync(ct);
    }

    private static NeighborhoodDto Map(Neighborhood n) => new(n.Id, n.Name, n.Region, n.IsActive);
}

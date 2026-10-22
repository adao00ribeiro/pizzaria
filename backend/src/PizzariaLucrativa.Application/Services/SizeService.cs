using Microsoft.EntityFrameworkCore;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Exceptions;
using PizzariaLucrativa.Application.Interfaces;
using PizzariaLucrativa.Domain.Entities;

namespace PizzariaLucrativa.Application.Services;

public sealed class SizeService : ISizeService
{
    private readonly IAppDbContext _db;
    private readonly ITenantProvider _tenant;

    public SizeService(IAppDbContext db, ITenantProvider tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<SizeDto>> ListAsync(CancellationToken ct = default)
    {
        var sizes = await _db.ToListAsync(_db.Set<Size>().OrderBy(s => s.Name));
        return sizes.Select(s => new SizeDto(s.Id, s.Name, s.DiameterCm)).ToList();
    }

    public async Task<SizeDto> CreateAsync(SizeInput input, CancellationToken ct = default)
    {
        var size = new Size
        {
            TenantId = _tenant.GetTenantId(),
            Name = input.Name.Trim(),
            DiameterCm = input.DiameterCm
        };
        _db.Add(size);
        await _db.SaveChangesAsync(ct);
        return new SizeDto(size.Id, size.Name, size.DiameterCm);
    }

    public async Task<SizeDto> UpdateAsync(Guid id, SizeInput input, CancellationToken ct = default)
    {
        var size = await FindAsync(id, ct);
        size.Name = input.Name.Trim();
        size.DiameterCm = input.DiameterCm;
        size.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return new SizeDto(size.Id, size.Name, size.DiameterCm);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var size = await FindAsync(id, ct);

        var used = await _db.Set<TechnicalSheet>().AnyAsync(s => s.SizeId == id)
                   || await _db.Set<ComboItem>().AnyAsync(c => c.SizeId == id);
        if (used)
            throw new ConflictException("Tamanho em uso por fichas técnicas ou combos. Desvincule antes de excluir.");

        _db.Remove(size);
        await _db.SaveChangesAsync(ct);
    }

    private async Task<Size> FindAsync(Guid id, CancellationToken ct)
    {
        var size = await _db.FirstOrDefaultAsync(_db.Set<Size>().Where(s => s.Id == id), ct);
        return size ?? throw new NotFoundException("Tamanho não encontrado.");
    }
}

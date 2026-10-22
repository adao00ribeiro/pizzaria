using Microsoft.EntityFrameworkCore;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Exceptions;
using PizzariaLucrativa.Application.Interfaces;
using PizzariaLucrativa.Domain.Entities;
using PizzariaLucrativa.Domain.Enums;

namespace PizzariaLucrativa.Application.Services;

/// <summary>Alertas de margem e estoque (docs/07 §11) — gerados pelo recálculo em cadeia.</summary>
public sealed class AlertService : IAlertService
{
    private readonly IAppDbContext _db;

    public AlertService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<AlertDto>> ListAsync(
        AlertStatus? status, AlertType? type, PagedRequest page, CancellationToken ct = default)
    {
        var query = _db.Set<Alert>().AsQueryable();
        if (status.HasValue)
            query = query.Where(a => a.Status == status.Value);
        if (type.HasValue)
            query = query.Where(a => a.Type == type.Value);

        var total = await _db.CountAsync(query, ct);
        var alerts = await _db.ToListAsync(
            query.OrderByDescending(a => a.Status)   // abertos primeiro
                .ThenByDescending(a => a.CreatedAt)
                .Skip(page.Skip)
                .Take(page.PageSize), ct);

        return new PagedResult<AlertDto>(alerts.Select(Map).ToList(), total);
    }

    public async Task<AlertDto> ResolveAsync(Guid id, CancellationToken ct = default)
    {
        var alert = await _db.FirstOrDefaultAsync(_db.Set<Alert>().Where(a => a.Id == id), ct)
            ?? throw new NotFoundException("Alerta não encontrado.");

        if (alert.Status == AlertStatus.Open)
        {
            alert.Status = AlertStatus.Resolved;
            alert.ResolvedAt = DateTime.UtcNow;
            alert.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        return Map(alert);
    }

    public async Task<AlertCountDto> CountAsync(CancellationToken ct = default)
    {
        var count = await _db.CountAsync(
            _db.Set<Alert>().Where(a => a.Status == AlertStatus.Open), ct);
        return new AlertCountDto(count);
    }

    private static AlertDto Map(Alert a) => new(
        a.Id, a.Type, a.Message, a.Status, a.CreatedAt, a.ResolvedAt);
}

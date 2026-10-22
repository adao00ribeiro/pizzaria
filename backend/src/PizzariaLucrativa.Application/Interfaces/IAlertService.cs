using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Domain.Enums;

namespace PizzariaLucrativa.Application.Interfaces;

public interface IAlertService
{
    Task<PagedResult<AlertDto>> ListAsync(AlertStatus? status, AlertType? type, PagedRequest page, CancellationToken ct = default);
    Task<AlertDto> ResolveAsync(Guid id, CancellationToken ct = default);
    Task<AlertCountDto> CountAsync(CancellationToken ct = default);
}

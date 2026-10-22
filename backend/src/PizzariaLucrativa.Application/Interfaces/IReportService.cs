using PizzariaLucrativa.Application.DTOs;

namespace PizzariaLucrativa.Application.Interfaces;

public interface IReportService
{
    Task<ProfitReportDto> GetProfitSummaryAsync(DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<IReadOnlyList<ProfitByDayDto>> GetProfitByDayAsync(DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<IReadOnlyList<ProfitByProductDto>> GetProfitByProductAsync(DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<IReadOnlyList<ProfitByChannelDto>> GetProfitByChannelAsync(DateTime? from, DateTime? to, CancellationToken ct = default);
}

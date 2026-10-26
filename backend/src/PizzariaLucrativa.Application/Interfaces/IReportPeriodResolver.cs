using PizzariaLucrativa.Application.DTOs;

namespace PizzariaLucrativa.Application.Interfaces;

/// <summary>
/// Resolve expressões de período ("hoje", "7 dias", "30 dias", "mês passado", "mês X",
/// "setembro") para UTC com início inclusivo e fim exclusivo (docs/sprints, IA-2).
/// </summary>
public interface IReportPeriodResolver
{
    AnalyticsPeriodDto Resolve(string? period, DateTime? referenceUtc = null);

    AnalyticsPeriodDto ResolveMonth(int year, int month);

    AnalyticsPeriodDto ResolveRange(DateTime fromLocal, DateTime toLocal);
}

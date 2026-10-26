using System.Globalization;
using PizzariaLucrativa.Application.Interfaces;

namespace PizzariaLucrativa.Application.Services;

/// <summary>
/// Fonte de tempo testável para relatórios e orçamento (docs/sprints, IA-1/IA-2).
/// </summary>
public sealed class DateTimeProvider : IDateTimeProvider
{
    private static readonly TimeZoneInfo SaoPauloTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public DateTime UtcNow => DateTime.UtcNow;

    public DateOnly TodayLocal => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZone));

    public TimeZoneInfo TimeZone => SaoPauloTimeZone;
}

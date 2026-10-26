using System.Globalization;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Exceptions;
using PizzariaLucrativa.Application.Interfaces;

namespace PizzariaLucrativa.Application.Services;

/// <summary>
/// Resolve expressões de período para UTC com início inclusivo e fim exclusivo no fuso America/Sao_Paulo.
/// </summary>
public sealed class ReportPeriodResolver : IReportPeriodResolver
{
    private static readonly TimeZoneInfo SaoPauloTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
    private readonly IDateTimeProvider _dateTimeProvider;

    public ReportPeriodResolver(IDateTimeProvider dateTimeProvider)
    {
        _dateTimeProvider = dateTimeProvider;
    }

    public AnalyticsPeriodDto Resolve(string? period, DateTime? referenceUtc = null)
    {
        var nowUtc = referenceUtc ?? _dateTimeProvider.UtcNow;
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), SaoPauloTimeZone);
        var todayLocal = DateOnly.FromDateTime(nowLocal);

        if (string.IsNullOrWhiteSpace(period))
            return ResolveRangeLocal(todayLocal.AddDays(-29), todayLocal);

        var p = period.Trim().ToLowerInvariant();

        if (p == "hoje")
            return ResolveRangeLocal(todayLocal, todayLocal);
        if (p == "ontem")
        {
            var yesterday = todayLocal.AddDays(-1);
            return ResolveRangeLocal(yesterday, yesterday);
        }
        if (p == "7dias" || p == "7d" || p == "7" || p == "ultimos7" || p == "últimos7" || p == "ultimos 7 dias")
            return ResolveRangeLocal(todayLocal.AddDays(-6), todayLocal);
        if (p == "30dias" || p == "30d" || p == "30" || p == "ultimos30" || p == "últimos30" || p == "ultimos 30 dias")
            return ResolveRangeLocal(todayLocal.AddDays(-29), todayLocal);

        if (p == "mespassado" || p == "mêspassado" || p == "lastmonth" || p == "mes_anterior")
            return ResolvePreviousMonth(todayLocal);

        // Try to parse month name or "mes X"
        // Try patterns like "2025-09" or "setembro 2025" or just month names
        if (TryParseMonthYear(p, out var y, out var m))
            return ResolveMonth(y, m);

        // Default to 30 days
        return ResolveRangeLocal(todayLocal.AddDays(-29), todayLocal);
    }

    public AnalyticsPeriodDto ResolveMonth(int year, int month)
    {
        if (month < 1 || month > 12)
            throw new ValidationException("month", "Mês inválido.");

        var startLocal = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var endLocalDate = new DateTime(year, month, DateTime.DaysInMonth(year, month), 0, 0, 0, DateTimeKind.Unspecified);
        return ResolveRangeLocal(DateOnly.FromDateTime(startLocal), DateOnly.FromDateTime(endLocalDate));
    }

    public AnalyticsPeriodDto ResolveRange(DateTime fromLocal, DateTime toLocal)
    {
        var fromDate = DateOnly.FromDateTime(fromLocal.Date);
        var toDate = DateOnly.FromDateTime(toLocal.Date);
        return ResolveRangeLocal(fromDate, toDate);
    }

    private AnalyticsPeriodDto ResolveRangeLocal(DateOnly fromLocal, DateOnly toLocal)
    {
        if (fromLocal > toLocal)
            throw new ValidationException("from", "A data inicial não pode ser posterior à data final.");

        var days = toLocal.DayNumber - fromLocal.DayNumber + 1;
        if (days > 366)
            throw new ValidationException("period", "O período máximo de consulta é de 366 dias.");

        var startLocalDateTime = new DateTime(fromLocal.Year, fromLocal.Month, fromLocal.Day, 0, 0, 0, DateTimeKind.Unspecified);
        var endLocalDateTime = new DateTime(toLocal.Year, toLocal.Month, toLocal.Day, 0, 0, 0, DateTimeKind.Unspecified).AddDays(1);

        var startUtc = TimeZoneInfo.ConvertTimeToUtc(startLocalDateTime, SaoPauloTimeZone);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(endLocalDateTime, SaoPauloTimeZone);

        return new AnalyticsPeriodDto(DateTime.SpecifyKind(startUtc, DateTimeKind.Utc), DateTime.SpecifyKind(endUtc, DateTimeKind.Utc));
    }

    private AnalyticsPeriodDto ResolvePreviousMonth(DateOnly todayLocal)
    {
        var firstOfThisMonth = new DateOnly(todayLocal.Year, todayLocal.Month, 1);
        var lastOfPrevMonth = firstOfThisMonth.AddDays(-1);
        var firstOfPrevMonth = new DateOnly(lastOfPrevMonth.Year, lastOfPrevMonth.Month, 1);
        return ResolveRangeLocal(firstOfPrevMonth, lastOfPrevMonth);
    }

    private static bool TryParseMonthYear(string p, out int year, out int month)
    {
        year = 0; month = 0;

        // Try yyyy-mm
        if (DateTime.TryParseExact(p, new[] { "yyyy-MM", "yyyy-M" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt1))
        {
            year = dt1.Year; month = dt1.Month; return true;
        }

        // Try month names in Portuguese
        var months = new[]
        {
            "janeiro", "fevereiro", "marco", "março", "abril", "maio", "junho",
            "julho", "agosto", "setembro", "outubro", "novembro", "dezembro"
        };
        for (int i = 0; i < months.Length; i++)
        {
            if (p.Contains(months[i]))
            {
                // try to find year
                var m = System.Text.RegularExpressions.Regex.Match(p, @"(20\d{2}|19\d{2})");
                if (m.Success)
                {
                    year = int.Parse(m.Groups[1].Value);
                    month = i < 12 ? (i % 12) + 1 : (i - 12) + 1;
                    // handle março
                    if (i == 2) month = 3;
                    if (i == 12) month = 3; // index off
                    // easier: fix by mapping
                    var mapping = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["janeiro"] = 1, ["fevereiro"] = 2, ["marco"] = 3, ["março"] = 3, ["abril"] = 4,
                        ["maio"] = 5, ["junho"] = 6, ["julho"] = 7, ["agosto"] = 8, ["setembro"] = 9,
                        ["outubro"] = 10, ["novembro"] = 11, ["dezembro"] = 12
                    };
                    foreach (var kv in mapping)
                        if (p.Contains(kv.Key)) { month = kv.Value; break; }
                    if (year > 0 && month >= 1 && month <= 12) return true;
                }
            }
        }
        return false;
    }
}

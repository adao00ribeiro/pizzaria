namespace PizzariaLucrativa.Application.Interfaces;

/// <summary>
/// Fonte de tempo testável para relatórios e orçamento (docs/sprints, IA-1/IA-2).
/// A implementação usa o fuso da pizzaria (<c>America/Sao_Paulo</c>).
/// </summary>
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }

    DateOnly TodayLocal { get; }

    TimeZoneInfo TimeZone { get; }
}

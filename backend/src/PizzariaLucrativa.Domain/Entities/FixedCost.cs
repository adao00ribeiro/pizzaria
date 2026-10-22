using PizzariaLucrativa.Domain.Enums;

namespace PizzariaLucrativa.Domain.Entities;

/// <summary>Custo fixo mensal (aluguel, energia, equipe) com método de rateio.</summary>
public class FixedCost : TenantEntity
{
    public string Description { get; set; } = string.Empty;

    public decimal MonthlyValue { get; set; }

    public AllocationType AllocationType { get; set; } = AllocationType.PerPizza;

    /// <summary>Exigido quando AllocationType = PerPizza.</summary>
    public int? ExpectedPizzasPerMonth { get; set; }

    /// <summary>Exigido quando AllocationType = RevenuePercentage.</summary>
    public decimal? ExpectedMonthlyRevenue { get; set; }
}

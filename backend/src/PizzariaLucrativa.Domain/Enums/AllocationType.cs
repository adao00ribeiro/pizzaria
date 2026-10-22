namespace PizzariaLucrativa.Domain.Enums;

/// <summary>Método de rateio de um custo fixo.</summary>
public enum AllocationType
{
    /// <summary>Valor fixo por pizza (valor mensal ÷ previsão de pizzas/mês).</summary>
    PerPizza = 0,

    /// <summary>Percentual sobre o faturamento esperado (reduz a margem disponível).</summary>
    RevenuePercentage = 1
}

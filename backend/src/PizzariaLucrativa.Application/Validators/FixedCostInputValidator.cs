using FluentValidation;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Domain.Enums;

namespace PizzariaLucrativa.Application.Validators;

public sealed class FixedCostInputValidator : AbstractValidator<FixedCostInput>
{
    public FixedCostInputValidator()
    {
        RuleFor(x => x.Description).NotEmpty().WithMessage("Informe a descrição do custo fixo.")
            .MaximumLength(120);
        RuleFor(x => x.MonthlyValue).GreaterThan(0).WithMessage("Valor mensal deve ser maior que zero.");
        RuleFor(x => x.ExpectedPizzasPerMonth)
            .NotNull().When(x => x.AllocationType == AllocationType.PerPizza)
            .WithMessage("Informe a previsão de pizzas por mês para o rateio PorPizza.")
            .Must(v => v is null or > 0).When(x => x.AllocationType == AllocationType.PerPizza)
            .WithMessage("Previsão de pizzas por mês deve ser maior que zero.");
        RuleFor(x => x.ExpectedMonthlyRevenue)
            .NotNull().When(x => x.AllocationType == AllocationType.RevenuePercentage)
            .WithMessage("Informe o faturamento mensal esperado para o rateio por percentual.")
            .Must(v => v is null or > 0).When(x => x.AllocationType == AllocationType.RevenuePercentage)
            .WithMessage("Faturamento mensal esperado deve ser maior que zero.");
    }
}

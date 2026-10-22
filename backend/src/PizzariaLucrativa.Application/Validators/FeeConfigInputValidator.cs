using FluentValidation;
using PizzariaLucrativa.Application.DTOs;

namespace PizzariaLucrativa.Application.Validators;

public sealed class FeeConfigInputValidator : AbstractValidator<FeeConfigDto>
{
    public FeeConfigInputValidator()
    {
        RuleFor(x => x.CardFeePercentage).InclusiveBetween(0, 100).WithMessage("Taxa de cartão deve estar entre 0% e 100%.");
        RuleFor(x => x.DeliveryFeePercentage).InclusiveBetween(0, 100).WithMessage("Taxa de delivery deve estar entre 0% e 100%.");
        RuleFor(x => x.DeliveryFixedValue).GreaterThanOrEqualTo(0).WithMessage("Valor fixo de entrega não pode ser negativo.");
        RuleFor(x => x.DefaultMarginPercentage).GreaterThan(0).LessThan(100).WithMessage("Margem desejada deve estar entre 0% e 100% (exclusive).");
        RuleFor(x => x.LossPercentageGlobal).InclusiveBetween(0, 100).WithMessage("Perda global deve estar entre 0% e 100%.");
    }
}

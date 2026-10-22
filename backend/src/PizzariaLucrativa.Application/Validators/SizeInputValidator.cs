using FluentValidation;
using PizzariaLucrativa.Application.DTOs;

namespace PizzariaLucrativa.Application.Validators;

public sealed class SizeInputValidator : AbstractValidator<SizeInput>
{
    public SizeInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Informe o nome do tamanho.").MaximumLength(60);
        RuleFor(x => x.DiameterCm).GreaterThan(0).When(x => x.DiameterCm.HasValue)
            .WithMessage("Diâmetro deve ser maior que zero.");
    }
}

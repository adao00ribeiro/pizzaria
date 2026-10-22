using FluentValidation;
using PizzariaLucrativa.Application.DTOs;

namespace PizzariaLucrativa.Application.Validators;

public sealed class HalfHalfInputValidator : AbstractValidator<HalfHalfInput>
{
    public HalfHalfInputValidator()
    {
        RuleFor(x => x.SizeId).NotEmpty().WithMessage("Selecione o tamanho.");
        RuleFor(x => x.ProductIds)
            .Must(ids => ids is { Count: 2 or 4 })
            .WithMessage("Selecione exatamente 2 sabores (meio a meio) ou 4 (quatro sabores).");
    }
}

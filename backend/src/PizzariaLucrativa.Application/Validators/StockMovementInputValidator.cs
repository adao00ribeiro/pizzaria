using FluentValidation;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Domain.Enums;

namespace PizzariaLucrativa.Application.Validators;

public sealed class StockMovementInputValidator : AbstractValidator<StockMovementInput>
{
    public StockMovementInputValidator()
    {
        RuleFor(x => x.IngredientId).NotEmpty().WithMessage("Selecione o ingrediente.");
        RuleFor(x => x.Quantity).NotEqual(0).WithMessage("Quantidade diferente de zero.");
        RuleFor(x => x.UnitCost).GreaterThanOrEqualTo(0).When(x => x.UnitCost.HasValue)
            .WithMessage("Custo unitário não pode ser negativo.");
        RuleFor(x => x.UnitCost).NotNull().When(x => x.Type == StockMovementType.In)
            .WithMessage("Informe o custo unitário da entrada.");
        RuleFor(x => x.Note).MaximumLength(200).WithMessage("Nota pode ter no máximo 200 caracteres.");
    }
}

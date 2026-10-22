using FluentValidation;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Domain.Enums;

namespace PizzariaLucrativa.Application.Validators;

public sealed class PreparationInputValidator : AbstractValidator<PreparationInput>
{
    public PreparationInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Informe o nome do preparo.")
            .MaximumLength(120).WithMessage("Nome pode ter no máximo 120 caracteres.");
        RuleFor(x => x.YieldQuantity).GreaterThan(0).WithMessage("Rendimento deve ser maior que zero.");
        RuleFor(x => x.Items).NotEmpty().WithMessage("Adicione ao menos um ingrediente ou preparo.");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Quantity).GreaterThan(0).WithMessage("Quantidade do item deve ser maior que zero.");
            item.RuleFor(i => i.Type)
                .Must((input, type) => type == SheetItemType.Ingredient
                    ? input.IngredientId.HasValue
                    : input.SourcePreparationId.HasValue)
                .WithMessage("Item deve referenciar um ingrediente ou um preparo.");
        });
    }
}

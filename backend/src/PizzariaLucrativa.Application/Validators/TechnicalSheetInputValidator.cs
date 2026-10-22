using FluentValidation;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Domain.Enums;

namespace PizzariaLucrativa.Application.Validators;

public sealed class TechnicalSheetInputValidator : AbstractValidator<TechnicalSheetInput>
{
    public TechnicalSheetInputValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("Selecione o produto (sabor).");
        RuleFor(x => x.SizeId).NotEmpty().WithMessage("Selecione o tamanho.");
        RuleFor(x => x.PackagingCost).GreaterThanOrEqualTo(0).WithMessage("Custo de embalagem não pode ser negativo.");
        RuleFor(x => x.LossPercentage).InclusiveBetween(0, 100).When(x => x.LossPercentage.HasValue)
            .WithMessage("Perda adicional deve estar entre 0% e 100%.");
        RuleFor(x => x.MarginPercentage).GreaterThan(0).LessThan(100).When(x => x.MarginPercentage.HasValue)
            .WithMessage("Margem da ficha deve estar entre 0% e 100% (exclusive).");
        RuleFor(x => x.Items).NotEmpty().WithMessage("Adicione ao menos um ingrediente ou preparo.");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Quantity).GreaterThan(0).WithMessage("Quantidade do item deve ser maior que zero.");
            item.RuleFor(i => i.Type)
                .Must((input, type) => type == SheetItemType.Ingredient
                    ? input.IngredientId.HasValue
                    : input.PreparationId.HasValue)
                .WithMessage("Item deve referenciar um ingrediente ou um preparo.");
        });
    }
}

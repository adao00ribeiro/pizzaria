using FluentValidation;
using PizzariaLucrativa.Application.DTOs;

namespace PizzariaLucrativa.Application.Validators;

public sealed class ComboInputValidator : AbstractValidator<ComboInput>
{
    public ComboInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Informe o nome do combo.").MaximumLength(120);
        RuleFor(x => x.DiscountValue).GreaterThanOrEqualTo(0).WithMessage("Desconto não pode ser negativo.");
        RuleFor(x => x.FixedPrice).GreaterThan(0).When(x => x.FixedPrice.HasValue)
            .WithMessage("Preço fixo deve ser maior que zero.");
        RuleFor(x => x.Items).NotEmpty().WithMessage("Adicione ao menos um item ao combo.");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).NotEmpty().WithMessage("Selecione o produto do item.");
            item.RuleFor(i => i.Quantity).GreaterThan(0).WithMessage("Quantidade deve ser maior que zero.");
        });
    }
}

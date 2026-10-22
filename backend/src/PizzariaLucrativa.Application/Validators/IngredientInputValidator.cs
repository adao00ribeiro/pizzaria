using FluentValidation;
using PizzariaLucrativa.Application.DTOs;

namespace PizzariaLucrativa.Application.Validators;

public sealed class IngredientInputValidator : AbstractValidator<IngredientInput>
{
    public IngredientInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Informe o nome do ingrediente.")
            .MaximumLength(120).WithMessage("Nome pode ter no máximo 120 caracteres.");
        RuleFor(x => x.PurchaseQuantity).GreaterThan(0).WithMessage("Quantidade de compra deve ser maior que zero.");
        RuleFor(x => x.PurchasePrice).GreaterThanOrEqualTo(0).WithMessage("Preço da compra não pode ser negativo.");
        RuleFor(x => x.LossPercentage).InclusiveBetween(0, 100).WithMessage("Perda deve estar entre 0% e 100%.");
        RuleFor(x => x.MinimumStock).GreaterThanOrEqualTo(0).WithMessage("Estoque mínimo não pode ser negativo.");
    }
}

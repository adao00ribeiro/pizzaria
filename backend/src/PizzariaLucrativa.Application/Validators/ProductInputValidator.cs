using FluentValidation;
using PizzariaLucrativa.Application.DTOs;

namespace PizzariaLucrativa.Application.Validators;

public sealed class ProductInputValidator : AbstractValidator<ProductInput>
{
    public ProductInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Informe o nome do produto.").MaximumLength(120);
        RuleFor(x => x.PurchasePrice).GreaterThanOrEqualTo(0).When(x => x.PurchasePrice.HasValue)
            .WithMessage("Preço de compra não pode ser negativo.");
        RuleFor(x => x.MarkupMultiplier).GreaterThan(0).When(x => x.MarkupMultiplier.HasValue)
            .WithMessage("Multiplicador de markup deve ser maior que zero.");
        RuleFor(x => x.CurrentStock).GreaterThanOrEqualTo(0).When(x => x.CurrentStock.HasValue)
            .WithMessage("Quantidade em estoque não pode ser negativa.");
        RuleFor(x => x.MinimumStock).GreaterThanOrEqualTo(0).When(x => x.MinimumStock.HasValue)
            .WithMessage("Estoque mínimo não pode ser negativo.");
        RuleFor(x => x.ImageUrl).MaximumLength(500)
            .WithMessage("URL da imagem não pode exceder 500 caracteres.");
    }
}

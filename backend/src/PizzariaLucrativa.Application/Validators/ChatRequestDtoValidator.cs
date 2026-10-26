using FluentValidation;
using PizzariaLucrativa.Application.DTOs;

namespace PizzariaLucrativa.Application.Validators;

public sealed class ChatRequestDtoValidator : AbstractValidator<ChatRequestDto>
{
    public ChatRequestDtoValidator()
    {
        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("A mensagem é obrigatória.")
            .MaximumLength(2000).WithMessage("A mensagem não pode exceder 2.000 caracteres.");
    }
}

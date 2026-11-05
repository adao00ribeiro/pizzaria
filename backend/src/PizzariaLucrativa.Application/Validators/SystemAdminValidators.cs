using FluentValidation;
using PizzariaLucrativa.Application.DTOs;

namespace PizzariaLucrativa.Application.Validators;

public sealed class TenantStatusUpdateInputValidator : AbstractValidator<TenantStatusUpdateInput>
{
    public TenantStatusUpdateInputValidator()
    {
        When(x => !x.IsActive, () =>
        {
            RuleFor(x => x.Reason)
                .NotEmpty().WithMessage("A justificativa é obrigatória para desativar a pizzaria.")
                .MinimumLength(5).WithMessage("A justificativa deve conter no mínimo 5 caracteres.")
                .MaximumLength(300).WithMessage("A justificativa deve conter no máximo 300 caracteres.");
        });

        When(x => x.IsActive && !string.IsNullOrWhiteSpace(x.Reason), () =>
        {
            RuleFor(x => x.Reason)
                .MaximumLength(300).WithMessage("A justificativa deve conter no máximo 300 caracteres.");
        });
    }
}

public sealed class SystemAdminLoginInputValidator : AbstractValidator<SystemAdminLoginInput>
{
    public SystemAdminLoginInputValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("O e-mail é obrigatório.")
            .EmailAddress().WithMessage("E-mail em formato inválido.")
            .MaximumLength(256);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("A senha é obrigatória.");
    }
}

public sealed class ForgotPasswordInputValidator : AbstractValidator<ForgotPasswordInput>
{
    public ForgotPasswordInputValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("O e-mail é obrigatório.")
            .EmailAddress().WithMessage("E-mail em formato inválido.")
            .MaximumLength(256);
    }
}

public sealed class ResetPasswordInputValidator : AbstractValidator<ResetPasswordInput>
{
    public ResetPasswordInputValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("O token de recuperação é obrigatório.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("A nova senha é obrigatória.")
            .MinimumLength(8).WithMessage("A senha deve ter no mínimo 8 caracteres.")
            .Matches(@"[A-Z]").WithMessage("A senha deve conter ao menos uma letra maiúscula.")
            .Matches(@"[0-9]").WithMessage("A senha deve conter ao menos um número.")
            .Matches(@"[^a-zA-Z0-9]").WithMessage("A senha deve conter ao menos um caractere especial.");
    }
}

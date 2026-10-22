using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc.Filters;
using FluentValidationException = FluentValidation.ValidationException;

namespace PizzariaLucrativa.Api.Filters;

/// <summary>
/// Executa os validadores FluentValidation registrados para cada argumento da action.
/// Sem isso as regras de negócio de entrada só seriam avaliadas pelos services —
/// e corpos inválidos chegariam ao cálculo, gerando 500 em vez do 400 previsto em docs/07.
/// </summary>
public sealed class ValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var services = context.HttpContext.RequestServices;
        var failures = new List<ValidationFailure>();

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
                continue;

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (services.GetService(validatorType) is not IValidator validator)
                continue;

            var validationContext = new ValidationContext<object>(argument);
            var result = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);

            if (!result.IsValid)
                failures.AddRange(result.Errors);
        }

        if (failures.Count > 0)
            throw new FluentValidationException(failures);

        await next();
    }
}

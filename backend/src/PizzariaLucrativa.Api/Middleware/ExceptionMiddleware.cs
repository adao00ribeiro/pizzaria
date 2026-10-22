using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using PizzariaLucrativa.Application.Exceptions;
using AppValidationException = PizzariaLucrativa.Application.Exceptions.ValidationException;
using FluentValidationException = FluentValidation.ValidationException;

namespace PizzariaLucrativa.Api.Middleware;

/// <summary>
/// Traduz exceções de aplicação/validação para o formato de erro padrão da API (docs/07):
/// { "status": 400, "message": "...", "errors": [{ "field": "...", "detail": "..." }] }.
/// </summary>
public sealed class ExceptionMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            await HandleAsync(context, exception);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        if (context.Response.HasStarted)
            throw exception;

        var (status, message, errors) = Map(exception);

        if (status >= 500)
            _logger.LogError(exception, "Erro não tratado em {Path}", context.Request.Path);
        else
            _logger.LogWarning(exception, "Rejeição em {Path}: {Status} {Message}", context.Request.Path, status, message);

        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";

        var payload = new ErrorEnvelope(status, message, errors);
        await context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions), context.RequestAborted);
    }

    private static (int Status, string Message, IReadOnlyList<ErrorItem> Errors) Map(Exception exception)
    {
        switch (exception)
        {
            case AppValidationException validation:
                return (400,
                    validation.Errors.Count > 0 ? "Erros de validação." : validation.Message,
                    validation.Errors.Select(e => new ErrorItem(e.Field, e.Detail)).ToList());

            case FluentValidationException fluent:
                return (400, "Erros de validação.",
                    fluent.Errors.Select(f => new ErrorItem(f.PropertyName, f.ErrorMessage)).ToList());

            case UnauthorizedException unauthorized:
                return (401, unauthorized.Message, Array.Empty<ErrorItem>());

            case NotFoundException notFound:
                return (404, notFound.Message, Array.Empty<ErrorItem>());

            case ConflictException conflict:
                return (409, conflict.Message, Array.Empty<ErrorItem>());

            // Violação de integridade do banco (FK ainda referenciada, índice único etc.)
            // é conflito de estado — 409, nunca 500.
            case DbUpdateConcurrencyException:
                return (409, "O registro foi alterado por outra requisição. Recarregue os dados e tente novamente.",
                    Array.Empty<ErrorItem>());

            case DbUpdateException update:
                return (409, ConstraintMessage(update), Array.Empty<ErrorItem>());

            case BadHttpRequestException badRequest:
                return (400, "Requisição inválida.", new[] { new ErrorItem("body", badRequest.Message) });

            case JsonException json:
                return (400, "JSON inválido.", new[] { new ErrorItem("body", json.Message) });

            case ArgumentException argument:
                return (400, argument.Message, Array.Empty<ErrorItem>());

            case InvalidOperationException invalidOperation:
                return (500, invalidOperation.Message, Array.Empty<ErrorItem>());

            default:
                return (500, "Erro interno do servidor.", Array.Empty<ErrorItem>());
        }
    }

    private static string ConstraintMessage(DbUpdateException exception)
    {
        var detail = exception.InnerException?.Message ?? exception.Message;

        if (detail.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
            || detail.Contains("duplicate key", StringComparison.OrdinalIgnoreCase))
            return "Já existe um registro com estes dados.";

        return "Não foi possível concluir a operação: o registro está em uso ou viola uma regra de integridade.";
    }

    private sealed record ErrorItem(string Field, string Detail);

    private sealed record ErrorEnvelope(int Status, string Message, IReadOnlyList<ErrorItem> Errors);
}

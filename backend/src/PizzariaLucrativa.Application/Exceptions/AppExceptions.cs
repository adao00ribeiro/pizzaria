namespace PizzariaLucrativa.Application.Exceptions;

/// <summary>Erro de validação de entrada → HTTP 400 com lista de campos (docs/07).</summary>
public sealed class ValidationException : Exception
{
    public IReadOnlyList<(string Field, string Detail)> Errors { get; }

    public ValidationException(string field, string detail) : base(detail)
    {
        Errors = new[] { (field, detail) };
    }

    public ValidationException(IReadOnlyList<(string Field, string Detail)> errors)
        : base(errors.FirstOrDefault().Detail ?? "Erros de validação.")
    {
        Errors = errors;
    }
}

/// <summary>Conflito de exclusão (RF-01/RF-02/RF-03) → HTTP 409.</summary>
public sealed class ConflictException : Exception
{
    public ConflictException(string message) : base(message)
    {
    }
}

/// <summary>Recurso não encontrado → HTTP 404.</summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message)
    {
    }
}

/// <summary>Não autenticado: credenciais inválidas, refresh token expirado ou sem tenant → HTTP 401.</summary>
public sealed class UnauthorizedException : Exception
{
    public UnauthorizedException(string message) : base(message)
    {
    }
}

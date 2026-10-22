namespace PizzariaLucrativa.Domain.Exceptions;

/// <summary>Violação de regra de negócio — mapeada para HTTP 400 com a mensagem em português.</summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }
}

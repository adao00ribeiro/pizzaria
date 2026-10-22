namespace PizzariaLucrativa.Domain.Exceptions;

/// <summary>
/// Preparos aninhados formaram um ciclo (NF-R04). Carrega a trilha de IDs visitados
/// para a mensagem de erro ser acionável.
/// </summary>
public class CycleDetectedException : DomainException
{
    public IReadOnlyList<Guid> Trail { get; }

    public CycleDetectedException(IReadOnlyList<Guid> trail)
        : base($"Ciclo detectado em preparos aninhados: {string.Join(" → ", trail.Select(id => id.ToString()))}")
    {
        Trail = trail;
    }
}

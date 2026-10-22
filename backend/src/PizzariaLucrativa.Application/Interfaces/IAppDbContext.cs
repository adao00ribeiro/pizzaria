namespace PizzariaLucrativa.Application.Interfaces;

/// <summary>
/// Abstração do DbContext para a camada Application não referenciar EF Core.
/// Implementado por AppDbContext (Infrastructure); os IQueryable repassam pelo
/// provider EF com o query filter de tenant aplicado.
/// </summary>
public interface IAppDbContext
{
    IQueryable<T> Set<T>() where T : class;

    void Add<T>(T entity) where T : class;

    void AddRange<T>(IEnumerable<T> entities) where T : class;

    void Remove<T>(T entity) where T : class;

    void RemoveRange<T>(IEnumerable<T> entities) where T : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Executa a ação dentro de transação (NF-R03).</summary>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default);

    /// <summary>Materializa consulta de forma assíncrona (a Application não referencia EF Core).</summary>
    Task<List<T>> ToListAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default);

    /// <summary>Conta registros da consulta de forma assíncrona.</summary>
    Task<int> CountAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default);

    /// <summary>Primeiro ou padrão, assíncrono.</summary>
    Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default);
}

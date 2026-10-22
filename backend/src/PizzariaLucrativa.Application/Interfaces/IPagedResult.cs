namespace PizzariaLucrativa.Application.Interfaces;

/// <summary>Resultado paginado envelopado (docs/07 — paginação server-side, NF-P05).</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total);

/// <summary>Parâmetros de paginação normalizados: página ≥ 1, tamanho entre 1 e 100 (padrão 20).</summary>
public readonly record struct PagedRequest
{
    public PagedRequest(int page = 1, int pageSize = 20)
    {
        Page = Math.Max(1, page);
        PageSize = Math.Clamp(pageSize, 1, 100);
    }

    public int Page { get; }

    public int PageSize { get; }

    public int Skip => (Page - 1) * PageSize;
}

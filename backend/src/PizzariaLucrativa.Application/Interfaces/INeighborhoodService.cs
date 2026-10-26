using PizzariaLucrativa.Application.DTOs;

namespace PizzariaLucrativa.Application.Interfaces;

/// <summary>
/// CRUD do catálogo de bairros/regiões do tenant (docs/sprints, IA-2).
/// A unicidade de nome é garantida no serviço por causa do TPT.
/// </summary>
public interface INeighborhoodService
{
    Task<IReadOnlyList<NeighborhoodDto>> ListAsync(
        bool? activeOnly = null, CancellationToken ct = default);

    Task<NeighborhoodDto> GetAsync(Guid id, CancellationToken ct = default);

    Task<NeighborhoodDto> CreateAsync(
        NeighborhoodInputDto input, CancellationToken ct = default);

    Task<NeighborhoodDto> UpdateAsync(
        Guid id, NeighborhoodInputDto input, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

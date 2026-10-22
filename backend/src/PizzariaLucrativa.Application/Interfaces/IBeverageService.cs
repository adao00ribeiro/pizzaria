using PizzariaLucrativa.Application.DTOs;

namespace PizzariaLucrativa.Application.Interfaces;

public interface IBeverageService
{
    /// <summary>Produtos Category=Beverage com preço calculado por markup (docs/06 §10).</summary>
    Task<IReadOnlyList<BeverageDto>> ListAsync(CancellationToken ct = default);
    Task<BeverageDto> UpdateMarkupAsync(Guid id, decimal markupMultiplier, decimal? purchasePrice = null, CancellationToken ct = default);
}

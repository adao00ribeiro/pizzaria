using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Domain.Enums;

namespace PizzariaLucrativa.Application.Interfaces;

public interface IProductService
{
    Task<IReadOnlyList<ProductDto>> ListAsync(ProductCategory? category, CancellationToken ct = default);
    Task<ProductDto> CreateAsync(ProductInput input, CancellationToken ct = default);
    Task<ProductDto> UpdateAsync(Guid id, ProductInput input, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

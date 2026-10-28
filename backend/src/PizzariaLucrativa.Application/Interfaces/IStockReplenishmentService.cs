using PizzariaLucrativa.Application.DTOs;

namespace PizzariaLucrativa.Application.Interfaces;

/// <summary>
/// Contrato de aplicação para emissão do relatório de reposição e compra de estoque de insumos.
/// </summary>
public interface IStockReplenishmentService
{
    /// <summary>
    /// Gera o relatório consolidado de reposição e compra de insumos aplicando os filtros opcionais.
    /// </summary>
    Task<StockReplenishmentReportDto> GetReplenishmentReportAsync(
        StockReplenishmentFilter? filter = null,
        CancellationToken ct = default);
}

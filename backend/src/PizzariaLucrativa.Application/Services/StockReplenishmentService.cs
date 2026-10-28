using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Interfaces;
using PizzariaLucrativa.Domain.Entities;
using PizzariaLucrativa.Domain.Enums;
using PizzariaLucrativa.Domain.Services;

namespace PizzariaLucrativa.Application.Services;

/// <summary>
/// Serviço de aplicação para cálculo e consolidação do relatório de reposição e compra de insumos.
/// Orquestra a consulta de ingredientes e delega as regras puras de cálculo para StockReplenishmentCalculator.
/// </summary>
public sealed class StockReplenishmentService : IStockReplenishmentService
{
    private readonly IAppDbContext _db;
    private readonly IDateTimeProvider _dateTimeProvider;

    public StockReplenishmentService(IAppDbContext db, IDateTimeProvider dateTimeProvider)
    {
        _db = db;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<StockReplenishmentReportDto> GetReplenishmentReportAsync(
        StockReplenishmentFilter? filter = null,
        CancellationToken ct = default)
    {
        var query = _db.Set<Ingredient>().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter?.Search))
        {
            var term = filter.Search.Trim().ToLower();
            query = query.Where(i => i.Name.ToLower().Contains(term));
        }

        var ingredients = await _db.ToListAsync(query, ct);

        var calculatedResults = ingredients.Select(i => StockReplenishmentCalculator.CalculateItem(
            i.Id,
            i.Name,
            i.PurchaseUnit,
            i.PurchaseQuantity,
            i.PurchasePrice,
            i.UnitCost,
            i.MinimumStock,
            i.CurrentStock
        )).ToList();

        var summary = StockReplenishmentCalculator.ConsolidateResults(calculatedResults);

        var itemsQuery = calculatedResults.AsEnumerable();

        if (filter?.Urgency is not null)
        {
            itemsQuery = itemsQuery.Where(i => i.Urgency == filter.Urgency.Value);
        }

        // Por padrão (se null) ou quando true, lista quem precisa de compra (urgência crítica, baixa ou alerta, ou déficit > 0)
        // Se Urgency foi especificado explicitamente e OnlyNeedingReplenishment não foi informado, preserva a Urgência solicitada
        var onlyNeeding = filter?.OnlyNeedingReplenishment ?? (filter?.Urgency is null);
        if (onlyNeeding)
        {
            itemsQuery = itemsQuery.Where(i => i.Urgency != StockReplenishmentUrgency.Normal || i.DeficitQuantity > 0m);
        }

        var orderedItems = itemsQuery
            .OrderBy(i => i.Urgency)
            .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .Select(i => new StockReplenishmentItemDto(
                i.IngredientId,
                i.Name,
                i.Unit,
                i.CurrentStock,
                i.MinimumStock,
                i.DeficitQuantity,
                i.SuggestedPackagesToBuy,
                i.SuggestedReplenishmentQuantity,
                i.PurchaseQuantity,
                i.PurchasePrice,
                i.UnitCost,
                i.EstimatedCost,
                i.Urgency))
            .ToList();

        return new StockReplenishmentReportDto(
            Items: orderedItems,
            TotalItemsNeedingReplenishment: summary.TotalItemsNeedingReplenishment,
            CriticalCount: summary.CriticalCount,
            LowCount: summary.LowCount,
            WarningCount: summary.WarningCount,
            TotalEstimatedCost: summary.TotalEstimatedCost,
            GeneratedAt: _dateTimeProvider.UtcNow);
    }
}

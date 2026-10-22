using Microsoft.EntityFrameworkCore;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Exceptions;
using PizzariaLucrativa.Application.Interfaces;
using PizzariaLucrativa.Domain.Entities;
using PizzariaLucrativa.Domain.Enums;

namespace PizzariaLucrativa.Application.Services;

/// <summary>
/// Estoque: saldo por insumo derivado dos movimentos (In soma, Out subtrai,
/// Adjustment define o saldo — inventário). Movimento abaixo do mínimo dispara
/// o recálculo em cadeia, que gera o Alert(LowStock) (docs/06 §11 passo 7).
/// </summary>
public sealed class StockService : IStockService
{
    private readonly IAppDbContext _db;
    private readonly ITenantProvider _tenant;
    private readonly IRecalculationQueue _recalculationQueue;

    public StockService(IAppDbContext db, ITenantProvider tenant, IRecalculationQueue recalculationQueue)
    {
        _db = db;
        _tenant = tenant;
        _recalculationQueue = recalculationQueue;
    }

    public async Task<PagedResult<StockBalanceDto>> BalancesAsync(
        bool? belowMinimum, PagedRequest page, CancellationToken ct = default)
    {
        var query = _db.Set<Ingredient>().AsQueryable();
        if (belowMinimum == true)
            query = query.Where(i => i.CurrentStock < i.MinimumStock);
        else if (belowMinimum == false)
            query = query.Where(i => i.CurrentStock >= i.MinimumStock);

        var total = await _db.CountAsync(query, ct);
        var ingredients = await _db.ToListAsync(
            query.OrderBy(i => i.CurrentStock < i.MinimumStock ? 0 : 1) // abaixo do mínimo primeiro
                .ThenBy(i => i.Name)
                .Skip(page.Skip)
                .Take(page.PageSize), ct);

        return new PagedResult<StockBalanceDto>(
            ingredients.Select(i => new StockBalanceDto(
                i.Id, i.Name, i.CurrentStock, i.MinimumStock,
                i.CurrentStock < i.MinimumStock, i.PurchaseUnit)).ToList(),
            total);
    }

    public async Task<StockMovementDto> CreateMovementAsync(StockMovementInput input, CancellationToken ct = default)
    {
        var ingredient = await _db.FirstOrDefaultAsync(
                _db.Set<Ingredient>().Where(i => i.Id == input.IngredientId), ct)
            ?? throw new NotFoundException("Ingrediente não encontrado.");

        var quantity = input.Type switch
        {
            StockMovementType.Out when input.Quantity < 0 =>
                throw new ValidationException("quantity", "Saída deve ser informada com valor positivo."),
            _ => input.Quantity
        };

        var movement = new StockMovement
        {
            TenantId = _tenant.GetTenantId(),
            IngredientId = ingredient.Id,
            Type = input.Type,
            Quantity = quantity,
            UnitCost = input.UnitCost,
            Note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim()
        };

        // Saldo derivado dos movimentos (docs/05 §7).
        ingredient.CurrentStock = input.Type switch
        {
            StockMovementType.In => ingredient.CurrentStock + quantity,
            StockMovementType.Out => ingredient.CurrentStock - quantity,
            _ => quantity // Adjustment: saldo apurado em inventário
        };
        ingredient.UpdatedAt = DateTime.UtcNow;

        _db.Add(movement);
        await _db.SaveChangesAsync(ct);

        // Pode cruzar o mínimo → gera Alert(LowStock) no recálculo em cadeia.
        await _recalculationQueue.EnqueueAsync(movement.TenantId, ct);

        return new StockMovementDto(
            movement.Id, ingredient.Name, movement.Type, movement.Quantity,
            movement.UnitCost, movement.Note, movement.CreatedAt);
    }

    public async Task<PagedResult<StockMovementDto>> MovementsAsync(
        Guid? ingredientId, PagedRequest page, CancellationToken ct = default)
    {
        var query = _db.Set<StockMovement>().AsQueryable();
        if (ingredientId.HasValue)
            query = query.Where(m => m.IngredientId == ingredientId.Value);

        var total = await _db.CountAsync(query, ct);
        var movements = await _db.ToListAsync(
            query.OrderByDescending(m => m.CreatedAt)
                .Skip(page.Skip)
                .Take(page.PageSize), ct);

        var ingredientIds = movements.Select(m => m.IngredientId).Distinct().ToList();
        var ingredients = await _db.ToListAsync(
            _db.Set<Ingredient>().Where(i => ingredientIds.Contains(i.Id))
                .Select(i => new { i.Id, i.Name }), ct);

        return new PagedResult<StockMovementDto>(
            movements.Select(m => new StockMovementDto(
                m.Id,
                ingredients.FirstOrDefault(i => i.Id == m.IngredientId)?.Name ?? "Ingrediente removido",
                m.Type, m.Quantity, m.UnitCost, m.Note, m.CreatedAt)).ToList(),
            total);
    }
}

using Microsoft.EntityFrameworkCore;
using PizzariaLucrativa.Application.Exceptions;
using PizzariaLucrativa.Application.Interfaces;
using PizzariaLucrativa.Domain.Entities;
using PizzariaLucrativa.Domain.Enums;
using PizzariaLucrativa.Domain.Exceptions;
using PizzariaLucrativa.Domain.Services;

namespace PizzariaLucrativa.Application.Services;

/// <summary>
/// Expande os itens do pedido até os insumos da ficha técnica e grava um
/// <see cref="StockMovement"/> do tipo <see cref="StockMovementType.OrderOut"/> por
/// ingrediente (docs/10, Sprint 2). Reaproveita <see cref="UnitConverter"/> e a
/// expansão recursiva de preparos (com detecção de ciclos).
/// </summary>
public sealed class StockConsumptionService : IStockConsumptionService
{
    private readonly IAppDbContext _db;

    public StockConsumptionService(IAppDbContext db) => _db = db;

    public async Task<bool> ConsumeForOrderAsync(Order order, CancellationToken ct = default)
    {
        // Idempotência (NF): um pedido baixa estoque uma única vez.
        var alreadyConsumed = await _db.Set<StockMovement>()
            .AnyAsync(m => m.OrderId == order.Id && m.Type == StockMovementType.OrderOut, ct);
        if (alreadyConsumed)
            return false;

        var ctx = new CalculationContext(_db);
        await ctx.LoadAsync(ct);

        // Acumula consumo por ingrediente, já convertido para a unidade de compra.
        var totals = new Dictionary<Guid, decimal>();
        foreach (var item in order.Items)
            await ExpandOrderItemAsync(item, ctx, totals, new List<Guid>(), ct);

        if (totals.Count == 0)
            return true;

        var now = DateTime.UtcNow;
        var note = $"Baixa do pedido #{order.Code}";

        foreach (var (ingredientId, quantity) in totals)
        {
            var rounded = Round4(quantity);
            if (rounded == 0)
                continue;

            var ingredient = await _db.FirstOrDefaultAsync(
                    _db.Set<Ingredient>().Where(i => i.Id == ingredientId), ct)
                ?? throw new ValidationException("items",
                    "Ingrediente da ficha não encontrado para baixa de estoque.");

            _db.Add(new StockMovement
            {
                TenantId = order.TenantId,
                IngredientId = ingredient.Id,
                Type = StockMovementType.OrderOut,
                Quantity = rounded,
                UnitCost = ingredient.UnitCost,
                OrderId = order.Id,
                Note = note
            });

            // Saldo derivado dos movimentos (docs/05 §7).
            ingredient.CurrentStock -= rounded;
            ingredient.UpdatedAt = now;

            // Se o estoque atingir ou ficar abaixo do mínimo, acende alerta de reposição imediatamente (docs/06 §11).
            if (ingredient.MinimumStock > 0 && ingredient.CurrentStock <= ingredient.MinimumStock)
            {
                var hasOpenAlert = await _db.Set<Alert>()
                    .AnyAsync(a => a.TenantId == order.TenantId &&
                                   a.IngredientId == ingredient.Id &&
                                   a.Type == AlertType.LowStock &&
                                   a.Status == AlertStatus.Open, ct);

                if (!hasOpenAlert)
                {
                    _db.Add(new Alert
                    {
                        TenantId = order.TenantId,
                        Type = AlertType.LowStock,
                        IngredientId = ingredient.Id,
                        Message = $"Estoque de '{ingredient.Name}' abaixo do mínimo " +
                                  $"({ingredient.CurrentStock} {ingredient.PurchaseUnit} <= {ingredient.MinimumStock} {ingredient.PurchaseUnit}).",
                        Status = AlertStatus.Open
                    });
                }
            }
        }

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> ReverseForOrderAsync(Order order, CancellationToken ct = default)
    {
        var consumed = await _db.ToListAsync(
            _db.Set<StockMovement>()
                .Where(m => m.OrderId == order.Id && m.Type == StockMovementType.OrderOut), ct);
        if (consumed.Count == 0)
            return false;

        var alreadyReversed = await _db.Set<StockMovement>()
            .AnyAsync(m => m.OrderId == order.Id && m.Type == StockMovementType.In, ct);
        if (alreadyReversed)
            return false;

        var now = DateTime.UtcNow;
        var note = $"Estorno do pedido #{order.Code}";

        foreach (var movement in consumed)
        {
            var ingredient = await _db.FirstOrDefaultAsync(
                    _db.Set<Ingredient>().Where(i => i.Id == movement.IngredientId), ct);
            if (ingredient is null)
                continue;

            _db.Add(new StockMovement
            {
                TenantId = order.TenantId,
                IngredientId = ingredient.Id,
                Type = StockMovementType.In,
                Quantity = movement.Quantity,
                UnitCost = ingredient.UnitCost,
                OrderId = order.Id,
                Note = note
            });

            ingredient.CurrentStock += movement.Quantity;
            ingredient.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ---------------------------------------------------------------- expansão

    private async Task ExpandOrderItemAsync(
        OrderItem item, CalculationContext ctx, Dictionary<Guid, decimal> totals,
        List<Guid> trail, CancellationToken ct)
    {
        if (item.ComboId is Guid comboId)
        {
            var comboItems = await _db.ToListAsync(
                _db.Set<ComboItem>().Where(ci => ci.ComboId == comboId), ct);

            foreach (var comboItem in comboItems)
            {
                var scale = item.Quantity * comboItem.Quantity;
                if (comboItem.SizeId is Guid comboSizeId)
                {
                    var sheet = await LoadSheetAsync(comboItem.ProductId, comboSizeId, ct);
                    await ExpandSheetAsync(sheet, scale, ctx, totals, trail, ct);
                }
                else
                {
                    await ConsumeBeverageAsync(comboItem.ProductId, scale, ctx, totals, ct);
                }
            }

            return;
        }

        foreach (var flavor in item.Flavors)
        {
            var share = flavor.SharePercentage / 100m;
            if (item.SizeId is Guid sizeId)
            {
                var sheet = await LoadSheetAsync(flavor.ProductId, sizeId, ct);
                await ExpandSheetAsync(sheet, item.Quantity * share, ctx, totals, trail, ct);
            }
            else
            {
                await ConsumeBeverageAsync(flavor.ProductId, item.Quantity * share, ctx, totals, ct);
            }
        }
    }

    private async Task ExpandSheetAsync(
        TechnicalSheet sheet, decimal scale, CalculationContext ctx,
        Dictionary<Guid, decimal> totals, List<Guid> trail, CancellationToken ct)
    {
        var items = await _db.ToListAsync(
            _db.Set<TechnicalSheetItem>().Where(ti => ti.TechnicalSheetId == sheet.Id), ct);

        foreach (var sheetItem in items)
        {
            if (sheetItem.Type == SheetItemType.Ingredient && sheetItem.IngredientId is Guid ingredientId)
            {
                Consume(ingredientId, sheetItem.Quantity * scale, sheetItem.Unit, ctx, totals);
            }
            else if (sheetItem.PreparationId is Guid preparationId)
            {
                var preparation = ctx.GetPreparation(preparationId)
                    ?? throw new ValidationException("items", "Preparo da ficha não encontrado.");
                ExpandPreparation(preparation, sheetItem.Quantity * scale, sheetItem.Unit, ctx, totals, trail);
            }
        }
    }

    /// <summary>
    /// Expande um preparo consumido em <paramref name="amount"/> <paramref name="amountUnit"/>:
    /// fator = (quantidade convertida para a unidade de rendimento) ÷ rendimento.
    /// </summary>
    private void ExpandPreparation(
        PreparationData preparation, decimal amount, MeasureUnit amountUnit,
        CalculationContext ctx, Dictionary<Guid, decimal> totals, List<Guid> trail)
    {
        if (trail.Contains(preparation.Id))
            throw new ValidationException("items",
                $"Ciclo detectado no preparo '{preparation.Name}'. Corrija a receita antes de vender.");
        if (trail.Count >= PricingCalculator.MaxPreparationDepth)
            throw new ValidationException("items",
                $"Preparo '{preparation.Name}' excede o limite de aninhamento.");
        if (preparation.YieldQuantity <= 0)
            throw new ValidationException("items",
                $"Preparo '{preparation.Name}': rendimento deve ser maior que zero.");

        var amountInYieldUnit = ConvertUnit(amount, amountUnit, preparation.YieldUnit);
        var factor = amountInYieldUnit / preparation.YieldQuantity;

        trail.Add(preparation.Id);
        try
        {
            foreach (var item in preparation.Items)
            {
                if (item.Type == SheetItemType.Ingredient && item.IngredientId is Guid ingredientId)
                {
                    Consume(ingredientId, item.Quantity * factor, item.Unit, ctx, totals);
                }
                else if (item.SourcePreparationId is Guid childId)
                {
                    var child = ctx.GetPreparation(childId)
                        ?? throw new ValidationException("items", "Preparo aninhado não encontrado.");
                    ExpandPreparation(child, item.Quantity * factor, item.Unit, ctx, totals, trail);
                }
            }
        }
        finally
        {
            trail.RemoveAt(trail.Count - 1);
        }
    }

    private async Task ConsumeBeverageAsync(
        Guid productId, decimal quantity, CalculationContext ctx,
        Dictionary<Guid, decimal> totals, CancellationToken ct)
    {
        var product = await _db.FirstOrDefaultAsync(
                _db.Set<Product>().Where(p => p.Id == productId), ct)
            ?? throw new ValidationException("items", "Produto não encontrado.");

        if (quantity == 0)
            return;

        // Se a bebida não tiver insumo vinculado ainda, garante a criação e vinculação
        if (product.IngredientId is not Guid ingredientId)
        {
            var ingredient = new Ingredient
            {
                TenantId = product.TenantId,
                Name = product.Name,
                PurchaseUnit = MeasureUnit.un,
                PurchaseQuantity = 1,
                PurchasePrice = product.PurchasePrice ?? 0m,
                UnitCost = product.PurchasePrice ?? 0m,
                LossPercentage = 0,
                CurrentStock = 0m,
                MinimumStock = 0m,
                MarkupMultiplier = product.MarkupMultiplier ?? 2.0m
            };
            _db.Add(ingredient);
            await _db.SaveChangesAsync(ct);
            product.IngredientId = ingredient.Id;
            ingredientId = ingredient.Id;

            ctx.AddIngredient(new IngredientData(
                ingredient.Id, ingredient.Name, ingredient.PurchaseUnit,
                ingredient.PurchaseQuantity, ingredient.PurchasePrice, ingredient.LossPercentage));
        }

        Consume(ingredientId, quantity, MeasureUnit.un, ctx, totals, allowDimensionless: true);
    }

    private void Consume(
        Guid ingredientId, decimal quantity, MeasureUnit unit,
        CalculationContext ctx, Dictionary<Guid, decimal> totals, bool allowDimensionless = false)
    {
        var ingredient = ctx.GetIngredient(ingredientId)
            ?? throw new ValidationException("items", "Ingrediente da ficha não encontrado.");

        decimal converted;
        if (unit == ingredient.PurchaseUnit)
        {
            converted = quantity;
        }
        else
        {
            try
            {
                converted = UnitConverter.Convert(quantity, unit, ingredient.PurchaseUnit);
            }
            catch (DomainException)
            {
                if (!allowDimensionless)
                    throw new ValidationException("items",
                        $"Não é possível converter '{unit}' para '{ingredient.PurchaseUnit}' " +
                        $"do ingrediente '{ingredient.Name}'.");
                // Bebida vinculada a um insumo de contagem (ex.: lata): 1 unidade por bebida.
                converted = quantity;
            }
        }

        totals[ingredientId] = totals.GetValueOrDefault(ingredientId) + converted;
    }

    private async Task<TechnicalSheet> LoadSheetAsync(Guid productId, Guid sizeId, CancellationToken ct)
        => await _db.FirstOrDefaultAsync(
                _db.Set<TechnicalSheet>().Where(s => s.ProductId == productId && s.SizeId == sizeId), ct)
            ?? throw new ValidationException("items",
                "Produto sem ficha técnica para o tamanho selecionado. Não é possível dar baixa.");

    private static decimal ConvertUnit(decimal quantity, MeasureUnit from, MeasureUnit to)
    {
        try
        {
            return UnitConverter.Convert(quantity, from, to);
        }
        catch (DomainException ex)
        {
            throw new ValidationException("items", ex.Message);
        }
    }

    private static decimal Round4(decimal value)
        => decimal.Round(value, 4, MidpointRounding.AwayFromZero);
}

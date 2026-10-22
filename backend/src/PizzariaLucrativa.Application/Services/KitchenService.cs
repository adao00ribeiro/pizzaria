using Microsoft.EntityFrameworkCore;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Interfaces;
using PizzariaLucrativa.Domain.Entities;
using PizzariaLucrativa.Domain.Enums;

namespace PizzariaLucrativa.Application.Services;

/// <summary>
/// Painel do pizzaiolo: pedidos ativos (Recebido, Em preparo, Pronto) com itens,
/// frações e receita resumida (somente leitura), sem custo/margem (docs/10, Sprint 3).
/// O polling de 8s do frontend consome esta rota.
/// </summary>
public sealed class KitchenService : IKitchenService
{
    /// <summary>Limite em minutos a partir do qual o card é sinalizado como atrasado.</summary>
    public const int LateAfterMinutes = 20;

    private readonly IAppDbContext _db;

    public KitchenService(IAppDbContext db) => _db = db;

    public async Task<KitchenQueueDto> QueueAsync(CancellationToken ct = default)
    {
        var active = new[]
        {
            OrderStatus.Received,
            OrderStatus.InPreparation,
            OrderStatus.Ready
        };

        var orders = await _db.ToListAsync(
            _db.Set<Order>()
                .Where(o => active.Contains(o.Status))
                .OrderBy(o => o.PlacedAt), ct);

        var counts = new KitchenCountsDto(
            orders.Count(o => o.Status == OrderStatus.Received),
            orders.Count(o => o.Status == OrderStatus.InPreparation),
            orders.Count(o => o.Status == OrderStatus.Ready),
            orders.Count);

        if (orders.Count == 0)
            return new KitchenQueueDto(Array.Empty<KitchenQueueItemDto>(), counts, DateTime.UtcNow);

        var orderIds = orders.Select(o => o.Id).ToList();
        var items = await _db.ToListAsync(
            _db.Set<OrderItem>().Where(i => orderIds.Contains(i.OrderId)), ct);
        var itemIds = items.Select(i => i.Id).ToList();

        var flavors = itemIds.Count == 0
            ? new List<OrderItemFlavor>()
            : await _db.ToListAsync(
                _db.Set<OrderItemFlavor>().Where(f => itemIds.Contains(f.OrderItemId)), ct);

        var comboIds = items.Where(i => i.ComboId.HasValue).Select(i => i.ComboId!.Value).Distinct().ToList();
        var combos = comboIds.Count == 0
            ? new List<Combo>()
            : await _db.ToListAsync(_db.Set<Combo>().Where(c => comboIds.Contains(c.Id)), ct);
        var comboItems = comboIds.Count == 0
            ? new List<ComboItem>()
            : await _db.ToListAsync(_db.Set<ComboItem>().Where(ci => comboIds.Contains(ci.ComboId)), ct);

        var productIds = flavors.Select(f => f.ProductId)
            .Concat(comboItems.Select(ci => ci.ProductId))
            .Distinct().ToList();
        var sizeIds = items.Where(i => i.SizeId.HasValue).Select(i => i.SizeId!.Value)
            .Concat(comboItems.Where(ci => ci.SizeId.HasValue).Select(ci => ci.SizeId!.Value))
            .Distinct().ToList();

        var products = productIds.Count == 0
            ? new List<Product>()
            : await _db.ToListAsync(_db.Set<Product>().Where(p => productIds.Contains(p.Id)), ct);
        var sizes = sizeIds.Count == 0
            ? new List<Size>()
            : await _db.ToListAsync(_db.Set<Size>().Where(s => sizeIds.Contains(s.Id)), ct);

        // Receita resumida (somente leitura): ficha técnica por (produto, tamanho).
        var recipes = await LoadRecipesAsync(productIds, sizeIds, ct);

        var now = DateTime.UtcNow;
        var queue = orders.Select(order =>
        {
            var elapsed = Math.Max(0, (int)Math.Floor(
                (now - (order.StartedAt ?? order.PlacedAt)).TotalMinutes));

            var orderItems = items.Where(i => i.OrderId == order.Id)
                .Select(i =>
                {
                    if (i.ComboId is Guid comboId)
                    {
                        var comboName = combos.FirstOrDefault(c => c.Id == comboId)?.Name ?? "Combo";
                        var comboFlavors = comboItems
                            .Where(ci => ci.ComboId == comboId)
                            .Select(ci =>
                            {
                                var prod = products.FirstOrDefault(p => p.Id == ci.ProductId);
                                var prodName = prod?.Name ?? "Produto";
                                var szName = ci.SizeId.HasValue
                                    ? sizes.FirstOrDefault(s => s.Id == ci.SizeId.Value)?.Name
                                    : null;
                                var label = szName is not null
                                    ? $"{ci.Quantity}x {prodName} ({szName})"
                                    : $"{ci.Quantity}x {prodName}";
                                var recipe = ci.SizeId.HasValue
                                    ? recipes.GetValueOrDefault((ci.ProductId, ci.SizeId.Value))
                                        ?? Array.Empty<KitchenRecipeLineDto>()
                                    : Array.Empty<KitchenRecipeLineDto>();
                                return new KitchenFlavorDto(label, 100m, recipe, prod?.ImageUrl);
                            })
                            .ToList();

                        return new KitchenItemDto(i.Quantity, null, comboFlavors, comboName, i.Notes);
                    }

                    var itemFlavors = flavors.Where(f => f.OrderItemId == i.Id)
                        .Select(f =>
                        {
                            var prod = products.FirstOrDefault(p => p.Id == f.ProductId);
                            return new KitchenFlavorDto(
                                prod?.Name ?? "Produto removido",
                                f.SharePercentage,
                                i.SizeId.HasValue
                                    ? recipes.GetValueOrDefault((f.ProductId, i.SizeId.Value))
                                        ?? Array.Empty<KitchenRecipeLineDto>()
                                    : Array.Empty<KitchenRecipeLineDto>(),
                                prod?.ImageUrl);
                        })
                        .ToList();

                    return new KitchenItemDto(
                        i.Quantity,
                        i.SizeId.HasValue ? sizes.FirstOrDefault(s => s.Id == i.SizeId)?.Name : null,
                        itemFlavors,
                        null,
                        i.Notes);
                })
                .ToList();

            return new KitchenQueueItemDto(
                order.Id, order.Code, order.Status, order.Type, order.CustomerName,
                order.PlacedAt, order.StartedAt, elapsed, elapsed >= LateAfterMinutes,
                orderItems, order.Notes, order.ReadyAt);
        }).ToList();

        return new KitchenQueueDto(queue, counts, now);
    }

    /// <summary>
    /// Monta a receita resumida (nome + quantidade + unidade) das fichas técnicas dos
    /// pares (produto, tamanho) usados nos itens — sem expor custo (docs/10, Sprint 3).
    /// </summary>
    private async Task<Dictionary<(Guid ProductId, Guid SizeId), IReadOnlyList<KitchenRecipeLineDto>>>
        LoadRecipesAsync(IReadOnlyList<Guid> productIds, IReadOnlyList<Guid> sizeIds, CancellationToken ct)
    {
        var result = new Dictionary<(Guid, Guid), IReadOnlyList<KitchenRecipeLineDto>>();
        if (productIds.Count == 0 || sizeIds.Count == 0)
            return result;

        var sheets = await _db.ToListAsync(
            _db.Set<TechnicalSheet>()
                .Where(s => productIds.Contains(s.ProductId) && sizeIds.Contains(s.SizeId)), ct);
        if (sheets.Count == 0)
            return result;

        var sheetIds = sheets.Select(s => s.Id).ToList();
        var sheetItems = await _db.ToListAsync(
            _db.Set<TechnicalSheetItem>().Where(ti => sheetIds.Contains(ti.TechnicalSheetId)), ct);

        var ingredientIds = sheetItems
            .Where(ti => ti.IngredientId.HasValue)
            .Select(ti => ti.IngredientId!.Value).Distinct().ToList();
        var preparationIds = sheetItems
            .Where(ti => ti.PreparationId.HasValue)
            .Select(ti => ti.PreparationId!.Value).Distinct().ToList();

        var ingredientNames = ingredientIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await _db.ToListAsync(
                    _db.Set<Ingredient>().Where(i => ingredientIds.Contains(i.Id)), ct))
                .ToDictionary(i => i.Id, i => i.Name);
        var preparationNames = preparationIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await _db.ToListAsync(
                    _db.Set<Preparation>().Where(p => preparationIds.Contains(p.Id)), ct))
                .ToDictionary(p => p.Id, p => p.Name);

        foreach (var sheet in sheets)
        {
            var lines = sheetItems
                .Where(ti => ti.TechnicalSheetId == sheet.Id)
                .Select(ti => new KitchenRecipeLineDto(
                    ti.Type == SheetItemType.Ingredient
                        ? ingredientNames.GetValueOrDefault(ti.IngredientId ?? Guid.Empty, "Ingrediente")
                        : preparationNames.GetValueOrDefault(ti.PreparationId ?? Guid.Empty, "Preparo"),
                    ti.Quantity,
                    ti.Unit))
                .ToList();

            result[(sheet.ProductId, sheet.SizeId)] = lines;
        }

        return result;
    }
}

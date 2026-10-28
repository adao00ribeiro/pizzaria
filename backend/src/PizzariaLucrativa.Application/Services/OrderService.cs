using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Exceptions;
using PizzariaLucrativa.Application.Interfaces;
using PizzariaLucrativa.Domain.Entities;
using PizzariaLucrativa.Domain.Enums;
using PizzariaLucrativa.Domain.Services;

namespace PizzariaLucrativa.Application.Services;

/// <summary>
/// Regras de pedido (docs/10, Sprints 2–7): criação com snapshot de preço/custo,
/// idempotência via <c>Idempotency-Key</c>, comanda imprimível, rastreio público e
/// máquina de status concorrente com baixa/estorno de estoque.
/// </summary>
public sealed partial class OrderService : IOrderService
{
    public const string TicketDisclaimer = "Não é documento fiscal";

    // Controle de idempotência por (TenantId, IdempotencyKey) e lock por pedido (Sprint 4 e Sprint 7).
    private static readonly ConcurrentDictionary<(Guid TenantId, string Key), Guid> IdempotencyMap = new();
    private static readonly ConcurrentDictionary<(Guid TenantId, string Key), SemaphoreSlim> IdempotencyLocks = new();
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> OrderTransitionLocks = new();

    private readonly IAppDbContext _db;
    private readonly ITenantProvider _tenant;
    private readonly ICurrentUser _currentUser;
    private readonly IStockConsumptionService _stock;
    private readonly IRecalculationQueue _recalculationQueue;

    public OrderService(
        IAppDbContext db,
        ITenantProvider tenant,
        ICurrentUser currentUser,
        IStockConsumptionService stock,
        IRecalculationQueue recalculationQueue)
    {
        _db = db;
        _tenant = tenant;
        _currentUser = currentUser;
        _stock = stock;
        _recalculationQueue = recalculationQueue;
    }

    // ---------------------------------------------------------------- criação

    public Task<OrderDto> CreateAsync(
        CreateOrderInput input,
        CancellationToken ct = default,
        string? idempotencyKey = null)
        => CreateCoreAsync(input, isPublic: false, idempotencyKey, ct);

    public Task<OrderDto> CreatePublicAsync(
        CreatePublicOrderInput input,
        CancellationToken ct = default,
        string? idempotencyKey = null)
    {
        if (input is null)
            throw new ValidationException("order", "Dados do pedido inválidos.");

        // Site público: exige nome e telefone válido em qualquer modalidade (Retirada ou Entrega).
        if (string.IsNullOrWhiteSpace(input.CustomerName))
            throw new ValidationException("customerName", "Informe o seu nome para concluir o pedido.");
        if (string.IsNullOrWhiteSpace(input.CustomerPhone) || !PhoneDigits().IsMatch(input.CustomerPhone))
            throw new ValidationException("customerPhone", "Informe um telefone válido com DDD.");

        var normalizedType = input.Type == OrderType.Counter ? OrderType.Pickup : input.Type;

        var createInput = new CreateOrderInput(
            Type: normalizedType,
            CustomerName: input.CustomerName,
            CustomerPhone: input.CustomerPhone,
            AddressDistrict: input.AddressDistrict,
            AddressStreet: input.AddressStreet,
            AddressNumber: input.AddressNumber,
            AddressComplement: input.AddressComplement,
            Notes: input.Notes,
            PaymentMethod: input.PaymentMethod,
            Discount: 0m, // Nunca aceita desconto vindo do cliente anônimo
            Items: input.Items,
            DeliveryAddress: input.DeliveryAddress,
            NeighborhoodId: input.NeighborhoodId);

        return CreateCoreAsync(createInput, isPublic: true, idempotencyKey, ct);
    }

    private async Task<OrderDto> CreateCoreAsync(
        CreateOrderInput input,
        bool isPublic,
        string? idempotencyKey,
        CancellationToken ct)
    {
        var normalizedInput = NormalizeAddress(input);
        var tenantId = _tenant.GetTenantId();
        var normalizedKey = Clean(idempotencyKey);

        if (normalizedKey is not null)
        {
            if (normalizedKey.Length > 100)
                throw new ValidationException("idempotencyKey", "Idempotency-Key deve ter no máximo 100 caracteres.");

            var keyTuple = (tenantId, normalizedKey);
            var keyGate = IdempotencyLocks.GetOrAdd(keyTuple, _ => new SemaphoreSlim(1, 1));
            await keyGate.WaitAsync(ct);
            try
            {
                if (IdempotencyMap.TryGetValue(keyTuple, out var existingId))
                {
                    var existing = await _db.FirstOrDefaultAsync(
                        _db.Set<Order>().Where(o => o.Id == existingId), ct);
                    if (existing is not null)
                        return await MapAsync(existing, ct);
                }

                var created = await PersistNewOrderAsync(normalizedInput, isPublic, tenantId, ct);
                IdempotencyMap[keyTuple] = created.Id;
                return created;
            }
            finally
            {
                keyGate.Release();
            }
        }

        return await PersistNewOrderAsync(normalizedInput, isPublic, tenantId, ct);
    }

    private async Task<OrderDto> PersistNewOrderAsync(
        CreateOrderInput input,
        bool isPublic,
        Guid tenantId,
        CancellationToken ct)
    {
        ValidateShape(input);

        var fees = await LoadFeesAsync(ct);
        var (neighborhoodId, regionSnapshot) = await ResolveNeighborhoodAsync(input.NeighborhoodId, input.AddressDistrict, ct);

        var order = new Order
        {
            TenantId = tenantId,
            Type = input.Type,
            Status = OrderStatus.Received,
            CustomerName = Clean(input.CustomerName),
            CustomerPhone = Clean(input.CustomerPhone),
            AddressDistrict = Clean(input.AddressDistrict),
            AddressStreet = Clean(input.AddressStreet),
            AddressNumber = Clean(input.AddressNumber),
            AddressComplement = Clean(input.AddressComplement),
            NeighborhoodId = neighborhoodId,
            RegionSnapshot = regionSnapshot,
            Notes = Clean(input.Notes),
            PaymentMethod = input.PaymentMethod,
            PlacedByUserId = isPublic ? null : _currentUser.UserId,
            PlacedAt = DateTime.UtcNow
        };

        var builtItems = new List<OrderItem>();
        foreach (var itemInput in input.Items)
        {
            var item = await BuildItemAsync(itemInput, ct);
            builtItems.Add(item);
            order.Items.Add(item);
        }

        // Usa a regra pura do Domain para totalização e validação de desconto (Sprint 4).
        (int Quantity, decimal UnitPrice)[] lines = builtItems
            .Select(i => (i.Quantity, i.UnitPrice))
            .ToArray();

        try
        {
            var totals = ProfitReportCalculator.CalculateOrderTotals(
                lines, input.Type, fees.DeliveryFixedValue, isPublic ? 0m : input.Discount);
            order.Subtotal = totals.Subtotal;
            order.DeliveryFee = totals.DeliveryFee;
            order.Discount = totals.Discount;
            order.Total = totals.Total;
        }
        catch (PizzariaLucrativa.Domain.Exceptions.DomainException ex)
        {
            throw new ValidationException("discount", ex.Message);
        }

        order.Code = await GenerateUniqueCodeAsync(ct);

        _db.Add(order);
        await _db.SaveChangesAsync(ct);

        return await MapAsync(order, ct);
    }

    private async Task<OrderItem> BuildItemAsync(CreateOrderItemInput input, CancellationToken ct)
    {
        var tenantId = _tenant.GetTenantId();

        if (input.Quantity <= 0 || input.Quantity > 50)
            throw new ValidationException("items", "A quantidade do item deve estar entre 1 e 50.");

        var item = new OrderItem
        {
            TenantId = tenantId,
            SizeId = input.SizeId,
            ComboId = input.ComboId,
            Quantity = input.Quantity,
            Notes = Clean(input.Notes)
        };

        if (input.ComboId is Guid comboId)
        {
            if (input.Flavors is not null && input.Flavors.Count > 0)
                throw new ValidationException("items", "Combo não aceita seleção de sabores.");

            var combo = await _db.FirstOrDefaultAsync(
                    _db.Set<Combo>().Where(c => c.Id == comboId), ct)
                ?? throw new ValidationException("items", "Combo não encontrado.");

            item.UnitPrice = combo.TotalPrice;
            item.CostSnapshot = combo.TotalCost;
        }
        else
        {
            if (input.Flavors is null || input.Flavors.Count is < 1 or > 4)
                throw new ValidationException("items", "Informe de 1 a 4 sabores por item.");

            var shareSum = input.Flavors.Sum(f => f.SharePercentage);
            if (Math.Abs(shareSum - 100m) > 0.01m)
                throw new ValidationException("items", "A soma das frações dos sabores deve ser 100%.");

            var productIds = input.Flavors.Select(f => f.ProductId).Distinct().ToList();
            var products = await _db.ToListAsync(
                _db.Set<Product>().Where(p => productIds.Contains(p.Id)), ct);
            if (products.Count != productIds.Count)
                throw new ValidationException("items", "Um ou mais produtos não foram encontrados.");

            decimal costSum = 0m;
            decimal price = 0m;

            foreach (var flavor in input.Flavors)
            {
                if (flavor.SharePercentage <= 0m)
                    throw new ValidationException("items", "Cada sabor deve ter fração maior que zero.");

                var product = products.First(p => p.Id == flavor.ProductId);

                if (input.SizeId is Guid sizeId)
                {
                    var sheet = await _db.FirstOrDefaultAsync(
                            _db.Set<TechnicalSheet>()
                                .Where(s => s.ProductId == product.Id && s.SizeId == sizeId), ct)
                        ?? throw new ValidationException("items",
                            $"O sabor '{product.Name}' não possui ficha técnica para o tamanho escolhido.");

                    costSum += sheet.TotalCost;
                    price = Math.Max(price, sheet.CardPrice ?? sheet.SuggestedPrice);
                }
                else
                {
                    if (product.Category != ProductCategory.Beverage)
                        throw new ValidationException("items", $"Selecione o tamanho do produto '{product.Name}'.");

                    var purchase = product.PurchasePrice ?? 0m;
                    var markup = product.MarkupMultiplier ?? 2.0m;
                    costSum += purchase;
                    price = Math.Max(price, Round2(purchase * markup));
                }

                item.Flavors.Add(new OrderItemFlavor
                {
                    TenantId = tenantId,
                    ProductId = product.Id,
                    SharePercentage = flavor.SharePercentage
                });
            }

            // Mista: preço pelo maior sabor, custo pela média (docs/06 §8).
            item.UnitPrice = price;
            item.CostSnapshot = Round4(costSum / input.Flavors.Count);
        }

        item.Total = Round2(item.UnitPrice * input.Quantity);
        return item;
    }

    // ---------------------------------------------------------------- consulta

    public async Task<PagedResult<OrderListItemDto>> ListAsync(
        OrderStatus? status, OrderType? type, DateTime? date, string? search,
        PagedRequest page, CancellationToken ct = default)
    {
        var query = _db.Set<Order>().AsQueryable();

        if (status.HasValue)
            query = query.Where(o => o.Status == status.Value);
        if (type.HasValue)
            query = query.Where(o => o.Type == type.Value);
        if (date.HasValue)
        {
            var day = date.Value.Date;
            var next = day.AddDays(1);
            query = query.Where(o => o.PlacedAt >= day && o.PlacedAt < next);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(o =>
                o.Code.ToLower().Contains(term)
                || (o.CustomerName != null && o.CustomerName.ToLower().Contains(term)));
        }

        var total = await _db.CountAsync(query, ct);
        var rows = await _db.ToListAsync(
            query.OrderByDescending(o => o.PlacedAt)
                .Skip(page.Skip)
                .Take(page.PageSize)
                .Select(o => new
                {
                    o.Id,
                    o.Code,
                    o.Status,
                    o.Type,
                    o.CustomerName,
                    o.Total,
                    o.PlacedAt,
                    o.ReadyAt,
                    ItemCount = o.Items.Count
                }), ct);

        return new PagedResult<OrderListItemDto>(
            rows.Select(r => new OrderListItemDto(
                r.Id, r.Code, r.Status, r.Type, r.CustomerName,
                r.Total, r.ItemCount, r.PlacedAt, r.ReadyAt)).ToList(),
            total);
    }

    public async Task<OrderSummaryDto> GetTodaySummaryAsync(CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var orders = await _db.ToListAsync(
            _db.Set<Order>()
                .Where(o => o.PlacedAt >= today && o.PlacedAt < tomorrow)
                .Select(o => new { o.Status, o.Type, o.Total }), ct);

        var activeOrCompletedRevenue = orders
            .Where(o => o.Status != OrderStatus.Cancelled)
            .Sum(o => o.Total);

        return new OrderSummaryDto(
            Received: orders.Count(o => o.Status == OrderStatus.Received),
            InPreparation: orders.Count(o => o.Status == OrderStatus.InPreparation),
            Ready: orders.Count(o => o.Status == OrderStatus.Ready),
            OutForDelivery: orders.Count(o => o.Status == OrderStatus.OutForDelivery),
            Completed: orders.Count(o => o.Status == OrderStatus.Completed),
            Cancelled: orders.Count(o => o.Status == OrderStatus.Cancelled),
            TotalOrders: orders.Count,
            TotalRevenue: Round2(activeOrCompletedRevenue),
            Counter: orders.Count(o => o.Type == OrderType.Counter),
            Pickup: orders.Count(o => o.Type == OrderType.Pickup),
            Delivery: orders.Count(o => o.Type == OrderType.Delivery));
    }

    public async Task<OrderDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var order = await _db.FirstOrDefaultAsync(_db.Set<Order>().Where(o => o.Id == id), ct)
            ?? throw new NotFoundException("Pedido não encontrado.");
        return await MapAsync(order, ct);
    }

    public async Task<OrderTicketDto> GetTicketAsync(Guid id, CancellationToken ct = default)
    {
        var order = await _db.FirstOrDefaultAsync(_db.Set<Order>().Where(o => o.Id == id), ct)
            ?? throw new NotFoundException("Pedido não encontrado.");

        var dto = await MapAsync(order, ct);
        var tenantId = _tenant.GetTenantId();
        var tenant = await _db.FirstOrDefaultAsync(
            _db.Set<Tenant>().Where(t => t.Id == tenantId), ct);

        var addressParts = new[]
        {
            !string.IsNullOrWhiteSpace(order.AddressStreet)
                ? $"{order.AddressStreet}, {order.AddressNumber ?? "s/n"}"
                : null,
            order.AddressComplement,
            order.AddressDistrict
        }.Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();

        var addressLine = addressParts.Length > 0 ? string.Join(" — ", addressParts) : null;

        var ticketItems = dto.Items.Select(i =>
        {
            var title = i.ComboName is not null
                ? i.ComboName
                : i.SizeName is not null
                    ? $"Pizza {i.SizeName}"
                    : i.Flavors.FirstOrDefault()?.ProductName ?? "Item";

            var flavors = i.Flavors
                .Select(f => new OrderTicketFlavorDto(f.ProductName, f.SharePercentage))
                .ToList();

            return new OrderTicketItemDto(
                i.Quantity,
                title,
                i.SizeName,
                i.ComboName,
                i.UnitPrice,
                i.Total,
                i.Notes,
                flavors);
        }).ToList();

        return new OrderTicketDto(
            OrderId: dto.Id,
            Code: dto.Code,
            PizzeriaName: tenant?.Name ?? "Pizzaria Lucrativa",
            PizzeriaSlug: tenant?.Slug,
            Status: dto.Status,
            Type: dto.Type,
            CustomerName: dto.CustomerName,
            CustomerPhone: dto.CustomerPhone,
            AddressLine: addressLine,
            PaymentMethod: dto.PaymentMethod,
            Notes: dto.Notes,
            PlacedAt: dto.PlacedAt,
            Subtotal: dto.Subtotal,
            DeliveryFee: dto.DeliveryFee,
            Discount: dto.Discount,
            Total: dto.Total,
            Disclaimer: TicketDisclaimer,
            Items: ticketItems);
    }

    public async Task<PublicMenuDto> GetCatalogAsync(CancellationToken ct = default)
    {
        var tenantId = _tenant.GetTenantId();
        var tenant = await _db.FirstOrDefaultAsync(
                _db.Set<Tenant>().Where(t => t.Id == tenantId), ct)
            ?? throw new NotFoundException("Pizzaria não encontrada.");

        var fees = await LoadFeesAsync(ct);

        var sizes = await _db.ToListAsync(
            _db.Set<Size>().OrderBy(s => s.DiameterCm), ct);
        var products = await _db.ToListAsync(
            _db.Set<Product>().Where(p => p.IsActive).OrderBy(p => p.Name), ct);
        var sheets = await _db.ToListAsync(
            _db.Set<TechnicalSheet>(), ct);
        var combos = await _db.ToListAsync(
            _db.Set<Combo>().OrderBy(c => c.Name), ct);
        var comboIds = combos.Select(c => c.Id).ToList();
        var comboItems = comboIds.Count == 0
            ? new List<ComboItem>()
            : await _db.ToListAsync(
                _db.Set<ComboItem>().Where(ci => comboIds.Contains(ci.ComboId)), ct);

        var sizeDtos = sizes
            .Select(s => new PublicSizeDto(s.Id, s.Name, s.DiameterCm ?? 0))
            .ToList();

        var pizzaDtos = products
            .Where(p => p.Category == ProductCategory.Pizza)
            .Select(p =>
            {
                var prices = sheets
                    .Where(sh => sh.ProductId == p.Id)
                    .Select(sh =>
                    {
                        var sz = sizes.FirstOrDefault(s => s.Id == sh.SizeId);
                        return sz is null
                            ? null
                            : new PublicPizzaPriceDto(
                                sz.Id,
                                sz.Name,
                                Round2(sh.CardPrice ?? sh.SuggestedPrice));
                    })
                    .Where(x => x is not null)
                    .Select(x => x!)
                    .ToList();

                return new PublicPizzaDto(p.Id, p.Name, prices, p.ImageUrl);
            })
            .Where(p => p.PricesBySize.Count > 0)
            .ToList();

        var comboDtos = combos
            .Select(c => new PublicComboDto(
                c.Id,
                c.Name,
                Round2(c.TotalPrice),
                comboItems
                    .Where(ci => ci.ComboId == c.Id)
                    .Select(ci => new PublicComboItemDto(
                        products.FirstOrDefault(p => p.Id == ci.ProductId)?.Name ?? "Item",
                        ci.SizeId.HasValue
                            ? sizes.FirstOrDefault(s => s.Id == ci.SizeId.Value)?.Name
                            : null,
                        ci.Quantity))
                    .ToList()))
            .ToList();

        var beverageDtos = products
            .Where(p => p.Category == ProductCategory.Beverage)
            .Select(p =>
            {
                var purchase = p.PurchasePrice ?? 0m;
                var markup = p.MarkupMultiplier ?? 2.0m;
                return new PublicBeverageDto(p.Id, p.Name, Round2(purchase * markup), p.ImageUrl);
            })
            .ToList();

        return new PublicMenuDto(
            TenantId: tenant.Id,
            Name: tenant.Name,
            Slug: tenant.Slug ?? string.Empty,
            DeliveryFee: Round2(fees.DeliveryFixedValue),
            Sizes: sizeDtos,
            Pizzas: pizzaDtos,
            Combos: comboDtos,
            Beverages: beverageDtos);
    }

    public async Task<PublicOrderTrackingDto> GetPublicTrackingAsync(string code, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new NotFoundException("Pedido não encontrado.");

        var normalized = code.Trim().TrimStart('#').ToUpperInvariant();
        var order = await _db.FirstOrDefaultAsync(
                _db.Set<Order>().Where(o => o.Code == normalized), ct)
            ?? throw new NotFoundException("Pedido não encontrado.");

        var dto = await MapAsync(order, ct);
        var tenantId = _tenant.GetTenantId();
        var tenant = await _db.FirstOrDefaultAsync(
            _db.Set<Tenant>().Where(t => t.Id == tenantId), ct);

        var items = dto.Items.Select(i =>
        {
            var title = i.ComboName is not null
                ? i.ComboName
                : i.SizeName is not null
                    ? $"Pizza {i.SizeName}"
                    : i.Flavors.FirstOrDefault()?.ProductName ?? "Item";

            var flavorParts = i.Flavors.Count > 1
                ? string.Join(", ", i.Flavors.Select(f => $"{Math.Round(f.SharePercentage, 0)}% {f.ProductName}"))
                : i.Flavors.FirstOrDefault()?.ProductName;

            var flavors = i.Flavors
                .Select(f => new OrderTicketFlavorDto(f.ProductName, f.SharePercentage))
                .ToList();

            return new PublicOrderTrackingItemDto(
                Quantity: i.Quantity,
                SizeName: i.SizeName,
                ComboName: i.ComboName,
                Flavors: flavors,
                Description: title,
                UnitPrice: i.UnitPrice,
                Total: i.Total,
                Notes: i.Notes,
                FlavorsLabel: flavorParts);
        }).ToList();

        var firstName = !string.IsNullOrWhiteSpace(order.CustomerName)
            ? order.CustomerName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0]
            : null;

        return new PublicOrderTrackingDto(
            PizzeriaName: tenant?.Name ?? "Pizzaria",
            Code: dto.Code,
            Status: dto.Status,
            Type: dto.Type,
            PlacedAt: dto.PlacedAt,
            StartedAt: dto.StartedAt,
            ReadyAt: dto.ReadyAt,
            DeliveredAt: dto.DeliveredAt,
            CancelledAt: dto.CancelledAt,
            CancelReason: dto.CancelReason,
            Subtotal: dto.Subtotal,
            DeliveryFee: dto.DeliveryFee,
            Total: dto.Total,
            Items: items,
            Discount: dto.Discount,
            CustomerFirstName: firstName,
            PaymentMethod: dto.PaymentMethod,
            Slug: tenant?.Slug);
    }

    // ---------------------------------------------------------------- status

    public async Task<OrderDto> ChangeStatusAsync(
        Guid id, OrderStatus newStatus, string? reason, CancellationToken ct = default)
    {
        // Sprint 7: serializa transições concorrentes do mesmo pedido (ex.: dois pizzaiolos
        // clicando "Pronto" ao mesmo tempo) garantindo uma única baixa de estoque.
        var gate = OrderTransitionLocks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            var order = await FindWithItemsAsync(id, ct);

            if (order.Status == newStatus)
                return await MapAsync(order, ct);

            ValidateTransition(order, newStatus, reason);

            var affectsStock = false;
            await _db.ExecuteInTransactionAsync(async innerCt =>
            {
                var now = DateTime.UtcNow;
                order.Status = newStatus;
                order.UpdatedAt = now;

                switch (newStatus)
                {
                    case OrderStatus.InPreparation:
                        order.StartedAt = now;
                        break;

                    case OrderStatus.Ready:
                        order.ReadyAt = now;
                        // Baixa de estoque no "Pronto" (premissa P3), idempotente.
                        affectsStock = await _stock.ConsumeForOrderAsync(order, innerCt);
                        break;

                    case OrderStatus.Completed:
                        order.DeliveredAt = now;
                        // Garante a baixa de estoque na finalização do pedido (idempotente caso já tenha baixado no Ready).
                        var consumedOnCompleted = await _stock.ConsumeForOrderAsync(order, innerCt);
                        affectsStock = affectsStock || consumedOnCompleted;
                        break;

                    case OrderStatus.Cancelled:
                        order.CancelledAt = now;
                        order.CancelReason = reason!.Trim();
                        // Cancelamento após baixa devolve o estoque.
                        affectsStock = await _stock.ReverseForOrderAsync(order, innerCt);
                        break;
                }

                await _db.SaveChangesAsync(innerCt);
            }, ct);

            // Baixa/estorno pode cruzar o mínimo → Alert(LowStock) no recálculo (docs/06 §11).
            if (affectsStock)
                await _recalculationQueue.EnqueueAsync(order.TenantId, ct);

            return await MapAsync(order, ct);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<OrderDto> FinishFromKitchenAsync(Guid id, CancellationToken ct = default)
    {
        var order = await _db.FirstOrDefaultAsync(_db.Set<Order>().Where(o => o.Id == id), ct)
            ?? throw new NotFoundException("Pedido não encontrado.");

        var nextStatus = order.Type == OrderType.Delivery
            ? OrderStatus.OutForDelivery
            : OrderStatus.Completed;

        return await ChangeStatusAsync(id, nextStatus, null, ct);
    }

    public Task<OrderDto> CancelAsync(Guid id, string? reason, CancellationToken ct = default)
        => ChangeStatusAsync(id, OrderStatus.Cancelled, reason, ct);

    private static void ValidateTransition(Order order, OrderStatus to, string? reason)
    {
        var allowed = order.Status switch
        {
            OrderStatus.Received => new[] { OrderStatus.InPreparation, OrderStatus.Cancelled },
            OrderStatus.InPreparation => new[] { OrderStatus.Ready, OrderStatus.Cancelled },
            OrderStatus.Ready => order.Type == OrderType.Delivery
                ? new[] { OrderStatus.OutForDelivery, OrderStatus.Cancelled }
                : new[] { OrderStatus.Completed, OrderStatus.Cancelled },
            OrderStatus.OutForDelivery => new[] { OrderStatus.Completed, OrderStatus.Cancelled },
            _ => Array.Empty<OrderStatus>()
        };

        if (!allowed.Contains(to))
            throw new ValidationException("status",
                $"Transição de '{order.Status}' para '{to}' não é permitida.");

        if (to == OrderStatus.Cancelled && string.IsNullOrWhiteSpace(reason))
            throw new ValidationException("reason", "Informe o motivo do cancelamento.");
    }

    // ---------------------------------------------------------------- internals

    private static void ValidateShape(CreateOrderInput input)
    {
        if (input.Items is null || input.Items.Count == 0)
            throw new ValidationException("items", "Adicione ao menos um item ao pedido.");

        if (input.Items.Count > 50)
            throw new ValidationException("items", "O pedido excede o limite de 50 itens.");

        if (input.Discount < 0)
            throw new ValidationException("discount", "Desconto não pode ser negativo.");

        if (input.Type != OrderType.Delivery)
            return;

        if (string.IsNullOrWhiteSpace(input.CustomerName))
            throw new ValidationException("customerName", "Informe o nome do cliente para entrega.");
        if (string.IsNullOrWhiteSpace(input.CustomerPhone) || !PhoneDigits().IsMatch(input.CustomerPhone))
            throw new ValidationException("customerPhone", "Informe um telefone válido para entrega.");
        if (string.IsNullOrWhiteSpace(input.AddressDistrict))
            throw new ValidationException("addressDistrict", "Informe o bairro de entrega.");
        if (string.IsNullOrWhiteSpace(input.AddressStreet))
            throw new ValidationException("addressStreet", "Informe a rua de entrega.");
        if (string.IsNullOrWhiteSpace(input.AddressNumber))
            throw new ValidationException("addressNumber", "Informe o número do endereço de entrega.");
    }

    private async Task<string> GenerateUniqueCodeAsync(CancellationToken ct)
    {
        for (var attempt = 0; attempt < 15; attempt++)
        {
            var code = OrderCodeGenerator.Generate();
            var exists = await _db.Set<Order>().AnyAsync(o => o.Code == code, ct);
            if (!exists)
                return code;
        }

        throw new ConflictException("Não foi possível gerar um código único para o pedido.");
    }

    private async Task<FeeConfig> LoadFeesAsync(CancellationToken ct)
    {
        var tenantId = _tenant.GetTenantId();
        return await _db.FirstOrDefaultAsync(
                   _db.Set<FeeConfig>().Where(f => f.TenantId == tenantId), ct)
               ?? new FeeConfig { TenantId = tenantId };
    }

    /// <summary>
    /// Valida o bairro informado contra o catálogo do tenant (o query filter já impede
    /// enxergar bairro de outro tenant) e congela a região no pedido.
    /// </summary>
    private async Task<(Guid? Id, NeighborhoodRegion? Region)> ResolveNeighborhoodAsync(
        Guid? neighborhoodId, string? addressDistrict = null, CancellationToken ct = default)
    {
        if (neighborhoodId.HasValue)
        {
            var neighborhood = await _db.FirstOrDefaultAsync(
                    _db.Set<Neighborhood>().Where(n => n.Id == neighborhoodId.Value), ct)
                ?? throw new ValidationException("neighborhoodId", "Bairro não encontrado.");

            if (!neighborhood.IsActive)
                throw new ValidationException("neighborhoodId", "O bairro informado está desativado.");

            return (neighborhood.Id, neighborhood.Region);
        }

        if (!string.IsNullOrWhiteSpace(addressDistrict))
        {
            var normalized = addressDistrict.Trim();
            var neighborhood = await _db.FirstOrDefaultAsync(
                _db.Set<Neighborhood>().Where(n => n.Name.ToLower() == normalized.ToLower()), ct);

            if (neighborhood != null)
            {
                if (!neighborhood.IsActive)
                    throw new ValidationException("addressDistrict", "O bairro informado está desativado.");
                return (neighborhood.Id, neighborhood.Region);
            }
        }

        return (null, null);
    }

    private async Task<Order> FindWithItemsAsync(Guid id, CancellationToken ct)
    {
        var order = await _db.FirstOrDefaultAsync(_db.Set<Order>().Where(o => o.Id == id), ct)
            ?? throw new NotFoundException("Pedido não encontrado.");

        var items = await _db.ToListAsync(
            _db.Set<OrderItem>().Where(i => i.OrderId == id), ct);
        var itemIds = items.Select(i => i.Id).ToList();
        var flavors = itemIds.Count == 0
            ? new List<OrderItemFlavor>()
            : await _db.ToListAsync(
                _db.Set<OrderItemFlavor>().Where(f => itemIds.Contains(f.OrderItemId)), ct);

        foreach (var item in items)
            item.Flavors = flavors.Where(f => f.OrderItemId == item.Id).ToList();

        order.Items = items;
        return order;
    }

    private async Task<OrderDto> MapAsync(Order order, CancellationToken ct)
    {
        var items = await _db.ToListAsync(
            _db.Set<OrderItem>().Where(i => i.OrderId == order.Id), ct);
        var itemIds = items.Select(i => i.Id).ToList();

        var flavors = itemIds.Count == 0
            ? new List<OrderItemFlavor>()
            : await _db.ToListAsync(
                _db.Set<OrderItemFlavor>().Where(f => itemIds.Contains(f.OrderItemId)), ct);

        var productIds = flavors.Select(f => f.ProductId).Distinct().ToList();
        var sizeIds = items.Where(i => i.SizeId.HasValue).Select(i => i.SizeId!.Value).Distinct().ToList();
        var comboIds = items.Where(i => i.ComboId.HasValue).Select(i => i.ComboId!.Value).Distinct().ToList();

        var products = productIds.Count == 0
            ? new List<Product>()
            : await _db.ToListAsync(_db.Set<Product>().Where(p => productIds.Contains(p.Id)), ct);
        var sizes = sizeIds.Count == 0
            ? new List<Size>()
            : await _db.ToListAsync(_db.Set<Size>().Where(s => sizeIds.Contains(s.Id)), ct);
        var combos = comboIds.Count == 0
            ? new List<Combo>()
            : await _db.ToListAsync(_db.Set<Combo>().Where(c => comboIds.Contains(c.Id)), ct);

        var itemDtos = items.Select(i => new OrderItemDto(
            i.Id,
            i.SizeId,
            i.SizeId.HasValue ? sizes.FirstOrDefault(s => s.Id == i.SizeId)?.Name : null,
            i.ComboId,
            i.ComboId.HasValue ? combos.FirstOrDefault(c => c.Id == i.ComboId)?.Name : null,
            i.Quantity,
            i.UnitPrice,
            i.Total,
            i.Notes,
            flavors.Where(f => f.OrderItemId == i.Id)
                .Select(f => new OrderItemFlavorDto(
                    f.ProductId,
                    products.FirstOrDefault(p => p.Id == f.ProductId)?.Name ?? "Produto removido",
                    f.SharePercentage))
                .ToList())).ToList();

        return new OrderDto(
            order.Id, order.Code, order.Status, order.Type,
            order.CustomerName, order.CustomerPhone,
            order.AddressDistrict, order.AddressStreet, order.AddressNumber, order.AddressComplement,
            order.Subtotal, order.DeliveryFee, order.Discount, order.Total,
            order.Notes, order.PaymentMethod,
            order.PlacedAt, order.StartedAt, order.ReadyAt, order.DeliveredAt, order.CancelledAt,
            order.CancelReason,
            itemDtos,
            order.NeighborhoodId,
            order.RegionSnapshot);
    }

    private static CreateOrderInput NormalizeAddress(CreateOrderInput input)
    {
        if (input.Type != OrderType.Delivery)
            return input;

        var street = Clean(input.AddressStreet);
        var number = Clean(input.AddressNumber);
        var district = Clean(input.AddressDistrict);
        var complement = Clean(input.AddressComplement);

        if ((string.IsNullOrWhiteSpace(street) || string.IsNullOrWhiteSpace(district) || string.IsNullOrWhiteSpace(number))
            && !string.IsNullOrWhiteSpace(input.DeliveryAddress))
        {
            var parsed = ParseDeliveryAddress(input.DeliveryAddress);
            street = !string.IsNullOrWhiteSpace(street) ? street : parsed.Street;
            number = !string.IsNullOrWhiteSpace(number) ? number : parsed.Number;
            district = !string.IsNullOrWhiteSpace(district) ? district : parsed.District;
            complement = !string.IsNullOrWhiteSpace(complement) ? complement : parsed.Complement;
        }

        return input with
        {
            AddressStreet = street,
            AddressNumber = number,
            AddressDistrict = district,
            AddressComplement = complement
        };
    }

    public static (string? Street, string? Number, string? District, string? Complement) ParseDeliveryAddress(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return (null, null, null, null);

        var trimmed = raw.Trim();
        var parts = trimmed.Split(new[] { ',', '-' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        string? street;
        string? number;
        string? district;
        string? complement = null;

        if (parts.Length == 1)
        {
            var match = Regex.Match(parts[0], @"^(.+?)(?:\s+n[º°]?\s*|\s+)(\d+[a-zA-Z]?)$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                street = match.Groups[1].Value.Trim();
                number = match.Groups[2].Value.Trim();
            }
            else
            {
                street = parts[0];
                number = "s/n";
            }
            district = "Centro";
        }
        else if (parts.Length == 2)
        {
            street = parts[0];
            if (parts[1].Any(char.IsDigit))
            {
                number = parts[1];
                district = "Centro";
            }
            else
            {
                var match = Regex.Match(parts[0], @"^(.+?)(?:\s+n[º°]?\s*|\s+)(\d+[a-zA-Z]?)$", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    street = match.Groups[1].Value.Trim();
                    number = match.Groups[2].Value.Trim();
                }
                else
                {
                    number = "s/n";
                }
                district = parts[1];
            }
        }
        else if (parts.Length == 3)
        {
            if (!parts[1].Any(char.IsDigit) && parts[0].Any(char.IsDigit))
            {
                var match = Regex.Match(parts[0], @"^(.+?)(?:\s+n[º°]?\s*|\s+)(\d+[a-zA-Z]?)$", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    street = match.Groups[1].Value.Trim();
                    number = match.Groups[2].Value.Trim();
                }
                else
                {
                    street = parts[0];
                    number = "s/n";
                }
                district = parts[1];
                complement = parts[2];
            }
            else
            {
                street = parts[0];
                number = parts[1];
                district = parts[2];
            }
        }
        else
        {
            street = parts[0];
            number = parts[1];
            district = parts[2];
            complement = string.Join(", ", parts.Skip(3));
        }

        return (street, number ?? "s/n", district ?? "Centro", complement);
    }

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static decimal Round2(decimal value)
        => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static decimal Round4(decimal value)
        => decimal.Round(value, 4, MidpointRounding.AwayFromZero);

    /// <summary>Valida telefone com 8 a 13 dígitos (ignora máscara).</summary>
    [GeneratedRegex(@"^\D*(?:\d\D*){8,13}$", RegexOptions.CultureInvariant)]
    private static partial Regex PhoneDigits();
}

using PizzariaLucrativa.Domain.Enums;

namespace PizzariaLucrativa.Application.DTOs;

// ================================================================ Entrada

/// <summary>Pedido recebido pela API (tablet do atendente — Sprint 4; site público — Sprint 5).</summary>
public sealed record CreateOrderInput(
    OrderType Type,
    string? CustomerName,
    string? CustomerPhone,
    string? AddressDistrict,
    string? AddressStreet,
    string? AddressNumber,
    string? AddressComplement,
    string? Notes,
    PaymentMethod? PaymentMethod,
    decimal Discount,
    IReadOnlyList<CreateOrderItemInput> Items);

/// <summary>
/// Item do pedido: pizza (SizeId + 1–4 sabores), bebida (sabor único, sem SizeId)
/// ou combo (ComboId).
/// </summary>
public sealed record CreateOrderItemInput(
    Guid? SizeId,
    Guid? ComboId,
    int Quantity,
    string? Notes,
    IReadOnlyList<CreateOrderFlavorInput> Flavors);

public sealed record CreateOrderFlavorInput(Guid ProductId, decimal SharePercentage);

/// <summary>Transição de status genérica (Sprint 4 usa a mesma máquina da cozinha).</summary>
public sealed record ChangeOrderStatusInput(OrderStatus Status, string? Reason);

public sealed record CancelOrderInput(string? Reason);

// ================================================================ Saída

/// <summary>
/// Detalhe do pedido. Não expõe custo — atendente/pizzaiolo não podem ver margem
/// (docs/10, Sprint 7). O custo real é consumido apenas pelo relatório de lucro.
/// </summary>
public sealed record OrderDto(
    Guid Id,
    string Code,
    OrderStatus Status,
    OrderType Type,
    string? CustomerName,
    string? CustomerPhone,
    string? AddressDistrict,
    string? AddressStreet,
    string? AddressNumber,
    string? AddressComplement,
    decimal Subtotal,
    decimal DeliveryFee,
    decimal Discount,
    decimal Total,
    string? Notes,
    PaymentMethod? PaymentMethod,
    DateTime PlacedAt,
    DateTime? StartedAt,
    DateTime? ReadyAt,
    DateTime? DeliveredAt,
    DateTime? CancelledAt,
    string? CancelReason,
    IReadOnlyList<OrderItemDto> Items);

public sealed record OrderItemDto(
    Guid Id,
    Guid? SizeId,
    string? SizeName,
    Guid? ComboId,
    string? ComboName,
    int Quantity,
    decimal UnitPrice,
    decimal Total,
    string? Notes,
    IReadOnlyList<OrderItemFlavorDto> Flavors);

public sealed record OrderItemFlavorDto(Guid ProductId, string ProductName, decimal SharePercentage);

public sealed record OrderListItemDto(
    Guid Id,
    string Code,
    OrderStatus Status,
    OrderType Type,
    string? CustomerName,
    decimal Total,
    int ItemCount,
    DateTime PlacedAt,
    DateTime? ReadyAt);

/// <summary>Resumo do dia por status e tipo (Sprint 4: cabeçalho da tela de pedidos).</summary>
public sealed record OrderSummaryDto(
    int Received,
    int InPreparation,
    int Ready,
    int OutForDelivery,
    int Completed,
    int Cancelled,
    int TotalOrders,
    decimal TotalRevenue,
    int Counter = 0,
    int Pickup = 0,
    int Delivery = 0);

/// <summary>Ticket/comanda imprimível do pedido (Sprint 4 — premissa P7, sem gateway).</summary>
public sealed record OrderTicketDto(
    Guid OrderId,
    string Code,
    string PizzeriaName,
    string? PizzeriaSlug,
    OrderStatus Status,
    OrderType Type,
    string? CustomerName,
    string? CustomerPhone,
    string? AddressLine,
    PaymentMethod? PaymentMethod,
    string? Notes,
    DateTime PlacedAt,
    decimal Subtotal,
    decimal DeliveryFee,
    decimal Discount,
    decimal Total,
    string Disclaimer,
    IReadOnlyList<OrderTicketItemDto> Items);

public sealed record OrderTicketItemDto(
    int Quantity,
    string Title,
    string? SizeName,
    string? ComboName,
    decimal UnitPrice,
    decimal Total,
    string? Notes,
    IReadOnlyList<OrderTicketFlavorDto> Flavors);

public sealed record OrderTicketFlavorDto(string ProductName, decimal SharePercentage);

// ================================================================ Cozinha

/// <summary>
/// Fila da produção (docs/10, Sprint 3): itens prontos para o painel kanban do
/// pizzaiolo, com contagem por status e o instante da consulta (para o polling).
/// </summary>
public sealed record KitchenQueueDto(
    IReadOnlyList<KitchenQueueItemDto> Items,
    KitchenCountsDto Counts,
    DateTime GeneratedAt);

public sealed record KitchenCountsDto(int Received, int InPreparation, int Ready, int Total);

public sealed record KitchenQueueItemDto(
    Guid Id,
    string Code,
    OrderStatus Status,
    OrderType Type,
    string? CustomerName,
    DateTime PlacedAt,
    DateTime? StartedAt,
    int ElapsedMinutes,
    bool Late,
    IReadOnlyList<KitchenItemDto> Items,
    string? Notes = null,
    DateTime? ReadyAt = null);

public sealed record KitchenItemDto(
    int Quantity,
    string? SizeName,
    IReadOnlyList<KitchenFlavorDto> Flavors,
    string? ComboName = null,
    string? Notes = null);

public sealed record KitchenFlavorDto(
    string ProductName,
    decimal SharePercentage,
    IReadOnlyList<KitchenRecipeLineDto> Recipe,
    string? ImageUrl = null);

/// <summary>Linha da receita resumida exibida no card da produção (sem custo).</summary>
public sealed record KitchenRecipeLineDto(string Label, decimal Quantity, MeasureUnit Unit);

// ================================================================ Site Público (Sprint 5)

/// <summary>Cardápio público da pizzaria (sem qualquer campo de custo ou margem).</summary>
public sealed record PublicMenuDto(
    Guid TenantId,
    string Name,
    string Slug,
    decimal DeliveryFee,
    IReadOnlyList<PublicSizeDto> Sizes,
    IReadOnlyList<PublicPizzaDto> Pizzas,
    IReadOnlyList<PublicComboDto> Combos,
    IReadOnlyList<PublicBeverageDto> Beverages);

public sealed record PublicSizeDto(Guid Id, string Name, int DiameterCm);

public sealed record PublicPizzaDto(
    Guid ProductId,
    string Name,
    IReadOnlyList<PublicPizzaPriceDto> PricesBySize,
    string? ImageUrl = null);

public sealed record PublicPizzaPriceDto(Guid SizeId, string SizeName, decimal Price);

public sealed record PublicComboDto(
    Guid Id,
    string Name,
    decimal Price,
    IReadOnlyList<PublicComboItemDto> Items);

public sealed record PublicComboItemDto(string ProductName, string? SizeName, int Quantity);

public sealed record PublicBeverageDto(Guid ProductId, string Name, decimal Price, string? ImageUrl = null);

/// <summary>
/// Checkout anônimo do site público (Sprint 5). Qualquer campo de preço/custo/desconto
/// enviado por clientes mal-intencionados é ignorado — o servidor recalcula tudo pelo catálogo.
/// </summary>
public sealed record CreatePublicOrderInput(
    OrderType Type,
    string? CustomerName,
    string? CustomerPhone,
    string? AddressDistrict,
    string? AddressStreet,
    string? AddressNumber,
    string? AddressComplement,
    string? Notes,
    PaymentMethod? PaymentMethod,
    IReadOnlyList<CreateOrderItemInput> Items);

/// <summary>
/// Status público do pedido por código (`GET /public/{slug}/orders/{code}`).
/// Não expõe telefone nem endereço do cliente a terceiros (Sprint 5).
/// </summary>
public sealed record PublicOrderTrackingDto(
    string PizzeriaName,
    string Code,
    OrderStatus Status,
    OrderType Type,
    DateTime PlacedAt,
    DateTime? StartedAt,
    DateTime? ReadyAt,
    DateTime? DeliveredAt,
    DateTime? CancelledAt,
    string? CancelReason,
    decimal Subtotal,
    decimal DeliveryFee,
    decimal Total,
    IReadOnlyList<PublicOrderTrackingItemDto> Items);

public sealed record PublicOrderTrackingItemDto(
    int Quantity,
    string? SizeName,
    string? ComboName,
    IReadOnlyList<OrderTicketFlavorDto> Flavors);

// ================================================================ Relatório de Lucro Real (Sprint 6)

/// <summary>Relatório consolidado de lucro real no período (só `owner` — docs/10, Sprint 6).</summary>
public sealed record ProfitReportDto(
    DateTime From,
    DateTime To,
    decimal Revenue,
    decimal IngredientCost,
    decimal StockOutCost,
    bool HasCostDivergence,
    decimal CostDivergenceAmount,
    decimal EstimatedCardFee,
    decimal GrossProfit,
    decimal GrossMarginPercentage,
    int CompletedOrdersCount,
    int CancelledOrdersCount,
    decimal AverageTicket,
    decimal MonthlyFixedCostTotal,
    decimal ProportionalFixedCost,
    decimal NetResultAfterFixedCosts);

public sealed record ProfitByDayDto(
    string Date,
    int OrdersCount,
    decimal Revenue,
    decimal Cost,
    decimal GrossProfit,
    decimal MarginPercentage);

public sealed record ProfitByProductDto(
    Guid ProductId,
    string ProductName,
    decimal QuantitySold,
    decimal Revenue,
    decimal Cost,
    decimal Profit,
    decimal MarginPercentage);

public sealed record ProfitByChannelDto(
    string Channel,
    int OrdersCount,
    decimal Revenue,
    decimal Cost,
    decimal Profit,
    decimal SharePercentage);

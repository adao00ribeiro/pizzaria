using PizzariaLucrativa.Domain.Enums;

namespace PizzariaLucrativa.Application.DTOs;

// ================================================================ Período

/// <summary>
/// Período já resolvido para UTC (início inclusivo, fim exclusivo) no fuso da pizzaria.
/// Contrato congelado na IA-1 para ser reutilizado por analytics, tools do Gemini e frontend.
/// </summary>
public sealed record AnalyticsPeriodDto(
    DateTime From,
    DateTime To,
    string TimeZoneId = "America/Sao_Paulo");

// ================================================================ Pedidos por região

/// <summary>Linha do ranking de pedidos por bairro/região (IA-2).</summary>
public sealed record OrdersByRegionItemDto(
    Guid? NeighborhoodId,
    string District,
    NeighborhoodRegion? Region,
    int OrdersCount,
    int CompletedOrdersCount,
    decimal Revenue,
    decimal AverageTicket,
    decimal SharePercentage)
{
    /// <summary>Rótulo amigável da região; pedidos sem snapshot caem em "Não classificado".</summary>
    public string RegionLabel => Region switch
    {
        NeighborhoodRegion.Center => "Centro",
        NeighborhoodRegion.North => "Norte",
        NeighborhoodRegion.South => "Sul",
        NeighborhoodRegion.East => "Leste",
        NeighborhoodRegion.West => "Oeste",
        NeighborhoodRegion.Other => "Outra",
        _ => "Não classificado"
    };
}

public sealed record OrdersByRegionDto(
    AnalyticsPeriodDto Period,
    IReadOnlyList<OrdersByRegionItemDto> Items,
    int TotalOrders,
    decimal TotalRevenue);

// ================================================================ Resumo de pedidos

/// <summary>
/// Resumo operacional do período (IA-2): status, canal, pagamento, faturamento concluído
/// e ticket médio. Somas dos grupos sempre fecham com o total.
/// </summary>
public sealed record OrdersSummaryDto(
    AnalyticsPeriodDto Period,
    int TotalOrders,
    int Received,
    int InPreparation,
    int Ready,
    int OutForDelivery,
    int Completed,
    int Cancelled,
    int Counter,
    int Pickup,
    int Delivery,
    int PublicSite,
    int Cash,
    int Card,
    int Pix,
    int PaymentNotInformed,
    decimal CompletedRevenue,
    decimal AverageTicket);

// ================================================================ Heatmap de vendas

/// <summary>Célula do heatmap dia da semana × hora local (IA-2).</summary>
public sealed record SalesHeatmapItemDto(
    DayOfWeek DayOfWeek,
    int Hour,
    int OrdersCount,
    decimal Revenue);

public sealed record SalesHeatmapDto(
    AnalyticsPeriodDto Period,
    IReadOnlyList<SalesHeatmapItemDto> Cells);

// ================================================================ Comparação de períodos

public sealed record PeriodMetricsDto(
    int OrdersCount,
    decimal Revenue,
    decimal GrossProfit,
    decimal GrossMarginPercentage,
    decimal AverageTicket);

public sealed record PeriodChangeDto(decimal Absolute, decimal Percentage);

public sealed record PeriodComparisonChangesDto(
    PeriodChangeDto Revenue,
    PeriodChangeDto GrossProfit,
    PeriodChangeDto OrdersCount,
    PeriodChangeDto AverageTicket);

public sealed record PeriodComparisonDto(
    AnalyticsPeriodDto PeriodA,
    AnalyticsPeriodDto PeriodB,
    PeriodMetricsDto A,
    PeriodMetricsDto B,
    PeriodComparisonChangesDto Changes);

// ================================================================ Bairros

public sealed record NeighborhoodDto(
    Guid Id,
    string Name,
    NeighborhoodRegion Region,
    bool IsActive);

public sealed record NeighborhoodInputDto(
    string Name,
    NeighborhoodRegion Region,
    bool IsActive = true);

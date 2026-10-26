using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Interfaces;

namespace PizzariaLucrativa.Application.Ai;

/// <summary>
/// Executa tools validadas chamadas pelo Gemini via function calling (IA3-05).
/// Valida limites, nunca vaza PII e devolve erro estruturado se os argumentos forem inválidos.
/// </summary>
public sealed class AiToolExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly IReportService _reportService;
    private readonly IAnalyticsService _analyticsService;
    private readonly IStockService _stockService;
    private readonly ILogger<AiToolExecutor> _logger;

    public AiToolExecutor(
        IReportService reportService,
        IAnalyticsService analyticsService,
        IStockService stockService,
        ILogger<AiToolExecutor> logger)
    {
        _reportService = reportService;
        _analyticsService = analyticsService;
        _stockService = stockService;
        _logger = logger;
    }

    public async Task<JsonElement> ExecuteAsync(
        string toolName,
        JsonElement arguments,
        CancellationToken ct = default)
    {
        try
        {
            return toolName.ToLowerInvariant() switch
            {
                "get_profit_summary" => await ExecuteProfitSummaryAsync(arguments, ct),
                "get_profit_by_day" => await ExecuteProfitByDayAsync(arguments, ct),
                "get_profit_by_product" => await ExecuteProfitByProductAsync(arguments, ct),
                "get_profit_by_channel" => await ExecuteProfitByChannelAsync(arguments, ct),
                "get_orders_by_region" => await ExecuteOrdersByRegionAsync(arguments, ct),
                "get_orders_summary" => await ExecuteOrdersSummaryAsync(arguments, ct),
                "get_sales_heatmap" => await ExecuteSalesHeatmapAsync(arguments, ct),
                "compare_periods" => await ExecuteComparePeriodsAsync(arguments, ct),
                "get_stock_alerts" => await ExecuteStockAlertsAsync(arguments, ct),
                _ => SerializeResult(new { error = $"Tool desconhecida ou não permitida: '{toolName}'." })
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha controlada ao executar tool {ToolName}.", toolName);
            return SerializeResult(new { error = $"Erro ao executar a ferramenta: {ex.Message}" });
        }
    }

    private async Task<JsonElement> ExecuteProfitSummaryAsync(JsonElement args, CancellationToken ct)
    {
        if (!TryGetDateRange(args, out var from, out var to, out var error))
        {
            return SerializeResult(new { error });
        }

        var result = await _reportService.GetProfitSummaryAsync(from, to, ct);
        return SerializeResult(result);
    }

    private async Task<JsonElement> ExecuteProfitByDayAsync(JsonElement args, CancellationToken ct)
    {
        if (!TryGetDateRange(args, out var from, out var to, out var error))
        {
            return SerializeResult(new { error });
        }

        var result = await _reportService.GetProfitByDayAsync(from, to, ct);
        return SerializeResult(result);
    }

    private async Task<JsonElement> ExecuteProfitByProductAsync(JsonElement args, CancellationToken ct)
    {
        if (!TryGetDateRange(args, out var from, out var to, out var error))
        {
            return SerializeResult(new { error });
        }

        var limit = GetLimit(args, defaultLimit: 10);
        var result = await _reportService.GetProfitByProductAsync(from, to, ct);
        var limited = result.Take(limit).ToList();
        return SerializeResult(limited);
    }

    private async Task<JsonElement> ExecuteProfitByChannelAsync(JsonElement args, CancellationToken ct)
    {
        if (!TryGetDateRange(args, out var from, out var to, out var error))
        {
            return SerializeResult(new { error });
        }

        var result = await _reportService.GetProfitByChannelAsync(from, to, ct);
        return SerializeResult(result);
    }

    private async Task<JsonElement> ExecuteOrdersByRegionAsync(JsonElement args, CancellationToken ct)
    {
        if (!TryGetDateRange(args, out var from, out var to, out var error))
        {
            return SerializeResult(new { error });
        }

        var limit = GetLimit(args, defaultLimit: 10);
        var period = new AnalyticsPeriodDto(from, to);
        var result = await _analyticsService.GetOrdersByRegionAsync(period, limit, ct);
        return SerializeResult(result);
    }

    private async Task<JsonElement> ExecuteOrdersSummaryAsync(JsonElement args, CancellationToken ct)
    {
        if (!TryGetDateRange(args, out var from, out var to, out var error))
        {
            return SerializeResult(new { error });
        }

        var period = new AnalyticsPeriodDto(from, to);
        var result = await _analyticsService.GetOrdersSummaryAsync(period, ct);
        return SerializeResult(result);
    }

    private async Task<JsonElement> ExecuteSalesHeatmapAsync(JsonElement args, CancellationToken ct)
    {
        if (!TryGetDateRange(args, out var from, out var to, out var error))
        {
            return SerializeResult(new { error });
        }

        var period = new AnalyticsPeriodDto(from, to);
        var result = await _analyticsService.GetSalesHeatmapAsync(period, ct);
        return SerializeResult(result);
    }

    private async Task<JsonElement> ExecuteComparePeriodsAsync(JsonElement args, CancellationToken ct)
    {
        if (!TryGetDate(args, "fromA", out var fromA) ||
            !TryGetDate(args, "toA", out var toA) ||
            !TryGetDate(args, "fromB", out var fromB) ||
            !TryGetDate(args, "toB", out var toB))
        {
            return SerializeResult(new { error = "Datas inválidas para compare_periods. Formato esperado: AAAA-MM-DD para fromA, toA, fromB e toB." });
        }

        if (toA < fromA || toB < fromB)
        {
            return SerializeResult(new { error = "A data final de cada período não pode ser anterior à data inicial." });
        }

        if ((toA - fromA).TotalDays > 366 || (toB - fromB).TotalDays > 366)
        {
            return SerializeResult(new { error = "O intervalo máximo para cada período de comparação é de 366 dias." });
        }

        if (toA.TimeOfDay == TimeSpan.Zero) toA = toA.Date.AddDays(1).AddTicks(-1);
        if (toB.TimeOfDay == TimeSpan.Zero) toB = toB.Date.AddDays(1).AddTicks(-1);

        var periodA = new AnalyticsPeriodDto(fromA, toA);
        var periodB = new AnalyticsPeriodDto(fromB, toB);

        var result = await _analyticsService.ComparePeriodsAsync(periodA, periodB, ct);
        return SerializeResult(result);
    }

    private async Task<JsonElement> ExecuteStockAlertsAsync(JsonElement args, CancellationToken ct)
    {
        var balances = await _stockService.BalancesAsync(belowMinimum: true, new PagedRequest(1, 50), ct);
        var items = balances.Items.Select(b => new
        {
            IngredientName = b.Name,
            Unit = b.Unit.ToString(),
            b.CurrentStock,
            b.MinimumStock,
            IsOutOfStock = b.CurrentStock <= 0,
            IsBelowMinimum = b.BelowMinimum
        }).ToList();

        return SerializeResult(new { alerts = items, total = items.Count });
    }

    private static bool TryGetDateRange(
        JsonElement args,
        out DateTime from,
        out DateTime to,
        out string? error)
    {
        from = default;
        to = default;
        error = null;

        if (!TryGetDate(args, "from", out from) || !TryGetDate(args, "to", out to))
        {
            error = "Datas 'from' e 'to' são obrigatórias no formato AAAA-MM-DD.";
            return false;
        }

        if (to < from)
        {
            error = "A data final ('to') não pode ser anterior à data inicial ('from').";
            return false;
        }

        if ((to - from).TotalDays > 366)
        {
            error = "O intervalo máximo permitido entre 'from' e 'to' é de 366 dias.";
            return false;
        }

        // Se a data final não tem hora especificada (ou 00:00:00), ajusta para o final do dia
        if (to.TimeOfDay == TimeSpan.Zero)
        {
            to = to.Date.AddDays(1).AddTicks(-1);
        }

        return true;
    }

    private static bool TryGetDate(JsonElement args, string propName, out DateTime date)
    {
        date = default;
        if (!args.TryGetProperty(propName, out var prop))
        {
            return false;
        }

        var str = prop.GetString();
        if (string.IsNullOrWhiteSpace(str))
        {
            return false;
        }

        return DateTime.TryParse(str, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out date) ||
               DateTime.TryParse(str, out date);
    }

    private static int GetLimit(JsonElement args, int defaultLimit = 10)
    {
        if (args.TryGetProperty("limit", out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var l))
            {
                if (l <= 0) return 1;
                if (l > 50) return 50;
                return l;
            }
        }
        return defaultLimit;
    }

    private static JsonElement SerializeResult<T>(T obj)
    {
        var json = JsonSerializer.Serialize(obj, JsonOptions);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }
}

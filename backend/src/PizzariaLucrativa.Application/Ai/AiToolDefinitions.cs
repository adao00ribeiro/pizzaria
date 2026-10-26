using System.Text.Json;
using PizzariaLucrativa.Application.DTOs;

namespace PizzariaLucrativa.Application.Ai;

/// <summary>
/// Declarações das tools permitidas para function calling no Google Gemini (docs/07-api.md).
/// Cada tool tem schema estrito com additionalProperties=false e sem tenantId nem SQL.
/// </summary>
public static class AiToolDefinitions
{
    private static JsonElement ParseJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    public static readonly ChatToolDeclaration GetProfitSummary = new(
        "get_profit_summary",
        "Obtém o resumo financeiro consolidado da pizzaria no período: faturamento, custo de insumos, margem, ticket médio e lucro líquido.",
        ParseJson("""
        {
          "type": "object",
          "properties": {
            "from": {
              "type": "string",
              "description": "Data inicial do período no formato AAAA-MM-DD (ex.: 2026-09-01)"
            },
            "to": {
              "type": "string",
              "description": "Data final do período no formato AAAA-MM-DD (ex.: 2026-09-30)"
            }
          },
          "required": ["from", "to"],
          "additionalProperties": false
        }
        """)
    );

    public static readonly ChatToolDeclaration GetProfitByDay = new(
        "get_profit_by_day",
        "Obtém o lucro e faturamento detalhados dia a dia dentro de um período (máx. 366 dias).",
        ParseJson("""
        {
          "type": "object",
          "properties": {
            "from": {
              "type": "string",
              "description": "Data inicial no formato AAAA-MM-DD"
            },
            "to": {
              "type": "string",
              "description": "Data final no formato AAAA-MM-DD"
            }
          },
          "required": ["from", "to"],
          "additionalProperties": false
        }
        """)
    );

    public static readonly ChatToolDeclaration GetProfitByProduct = new(
        "get_profit_by_product",
        "Lista os produtos com maior lucro ou vendas no período informado.",
        ParseJson("""
        {
          "type": "object",
          "properties": {
            "from": {
              "type": "string",
              "description": "Data inicial no formato AAAA-MM-DD"
            },
            "to": {
              "type": "string",
              "description": "Data final no formato AAAA-MM-DD"
            },
            "limit": {
              "type": "integer",
              "description": "Quantidade máxima de produtos no ranking (entre 1 e 50, padrão 10)"
            }
          },
          "required": ["from", "to"],
          "additionalProperties": false
        }
        """)
    );

    public static readonly ChatToolDeclaration GetProfitByChannel = new(
        "get_profit_by_channel",
        "Obtém métricas de lucro e vendas agrupadas por canal de venda (Balcão, Salão, Delivery, WhatsApp).",
        ParseJson("""
        {
          "type": "object",
          "properties": {
            "from": {
              "type": "string",
              "description": "Data inicial no formato AAAA-MM-DD"
            },
            "to": {
              "type": "string",
              "description": "Data final no formato AAAA-MM-DD"
            }
          },
          "required": ["from", "to"],
          "additionalProperties": false
        }
        """)
    );

    public static readonly ChatToolDeclaration GetOrdersByRegion = new(
        "get_orders_by_region",
        "Obtém a distribuição de pedidos e faturamento por bairro/região da cidade.",
        ParseJson("""
        {
          "type": "object",
          "properties": {
            "from": {
              "type": "string",
              "description": "Data inicial no formato AAAA-MM-DD"
            },
            "to": {
              "type": "string",
              "description": "Data final no formato AAAA-MM-DD"
            },
            "limit": {
              "type": "integer",
              "description": "Quantidade de bairros/regiões a retornar (entre 1 e 50, padrão 10)"
            }
          },
          "required": ["from", "to"],
          "additionalProperties": false
        }
        """)
    );

    public static readonly ChatToolDeclaration GetOrdersSummary = new(
        "get_orders_summary",
        "Obtém métricas operacionais consolidadas dos pedidos: total de pedidos, ticket médio, cancelamentos e tempo médio.",
        ParseJson("""
        {
          "type": "object",
          "properties": {
            "from": {
              "type": "string",
              "description": "Data inicial no formato AAAA-MM-DD"
            },
            "to": {
              "type": "string",
              "description": "Data final no formato AAAA-MM-DD"
            }
          },
          "required": ["from", "to"],
          "additionalProperties": false
        }
        """)
    );

    public static readonly ChatToolDeclaration GetSalesHeatmap = new(
        "get_sales_heatmap",
        "Obtém a intensidade de vendas em cada dia da semana e hora do dia (mapa de calor 7x24).",
        ParseJson("""
        {
          "type": "object",
          "properties": {
            "from": {
              "type": "string",
              "description": "Data inicial no formato AAAA-MM-DD"
            },
            "to": {
              "type": "string",
              "description": "Data final no formato AAAA-MM-DD"
            }
          },
          "required": ["from", "to"],
          "additionalProperties": false
        }
        """)
    );

    public static readonly ChatToolDeclaration ComparePeriods = new(
        "compare_periods",
        "Compara dois períodos operacionais (ex.: mês atual vs mês anterior).",
        ParseJson("""
        {
          "type": "object",
          "properties": {
            "fromA": {
              "type": "string",
              "description": "Data inicial do período A no formato AAAA-MM-DD"
            },
            "toA": {
              "type": "string",
              "description": "Data final do período A no formato AAAA-MM-DD"
            },
            "fromB": {
              "type": "string",
              "description": "Data inicial do período B no formato AAAA-MM-DD"
            },
            "toB": {
              "type": "string",
              "description": "Data final do período B no formato AAAA-MM-DD"
            }
          },
          "required": ["fromA", "toA", "fromB", "toB"],
          "additionalProperties": false
        }
        """)
    );

    public static readonly ChatToolDeclaration GetStockAlerts = new(
        "get_stock_alerts",
        "Lista insumos com estoque crítico ou zerado que necessitam de reposição.",
        ParseJson("""
        {
          "type": "object",
          "properties": {
            "status": {
              "type": "string",
              "description": "Filtro opcional de status: 'critical', 'empty' ou 'all' (padrão 'all')"
            }
          },
          "additionalProperties": false
        }
        """)
    );

    public static readonly IReadOnlyList<ChatToolDeclaration> All = new[]
    {
        GetProfitSummary,
        GetProfitByDay,
        GetProfitByProduct,
        GetProfitByChannel,
        GetOrdersByRegion,
        GetOrdersSummary,
        GetSalesHeatmap,
        ComparePeriods,
        GetStockAlerts
    };
}

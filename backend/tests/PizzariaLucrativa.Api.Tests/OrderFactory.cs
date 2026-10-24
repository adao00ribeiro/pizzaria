using System.Net.Http.Json;
using System.Text.Json;

namespace PizzariaLucrativa.Api.Tests;

/// <summary>
/// Fábrica de dados de teste (docs/10, Sprints 2/3): cria catálogo mínimo e pedidos
/// via HTTP, exercitando o pipeline real. Usada por <c>OrderFlowTests</c> e
/// <c>KitchenQueueTests</c>.
/// </summary>
internal static class OrderFactory
{
    public static async Task<Guid> CreateIngredientAsync(
        HttpClient client, string token, string name,
        string purchaseUnit, decimal purchaseQuantity, decimal purchasePrice)
    {
        var response = await client.WithToken(token).PostAsJsonAsync("/api/v1/ingredients", new
        {
            name,
            purchaseUnit,
            purchaseQuantity,
            purchasePrice,
            lossPercentage = 0m,
            minimumStock = 0m
        });
        response.EnsureSuccessStatusCode();

        using var json = await TestClient.ReadJsonAsync(response);
        return json.RootElement.GetProperty("id").GetGuid();
    }

    public static async Task AddInitialStockAsync(
        HttpClient client, string token, Guid ingredientId, decimal quantity)
    {
        var response = await client.WithToken(token).PostAsJsonAsync("/api/v1/stock/movements", new
        {
            ingredientId,
            type = "In",
            quantity,
            unitCost = 0m,
            note = "carga inicial de teste"
        });
        response.EnsureSuccessStatusCode();
    }

    public static async Task<Guid> CreateSizeAsync(HttpClient client, string token, string name)
    {
        var response = await client.WithToken(token)
            .PostAsJsonAsync("/api/v1/sizes", new { name, diameterCm = 35 });
        response.EnsureSuccessStatusCode();

        using var json = await TestClient.ReadJsonAsync(response);
        return json.RootElement.GetProperty("id").GetGuid();
    }

    public static async Task<Guid> CreatePizzaAsync(HttpClient client, string token, string name, string? imageUrl = null)
    {
        var response = await client.WithToken(token).PostAsJsonAsync("/api/v1/products", new
        {
            name,
            category = "Pizza",
            isActive = true,
            imageUrl
        });
        response.EnsureSuccessStatusCode();

        using var json = await TestClient.ReadJsonAsync(response);
        return json.RootElement.GetProperty("id").GetGuid();
    }

    public static async Task<Guid> CreateBeverageAsync(
        HttpClient client, string token, string name, decimal purchasePrice,
        decimal markupMultiplier = 2.0m, string? imageUrl = null)
    {
        var response = await client.WithToken(token).PostAsJsonAsync("/api/v1/products", new
        {
            name,
            category = "Beverage",
            isActive = true,
            purchasePrice,
            markupMultiplier,
            imageUrl
        });
        response.EnsureSuccessStatusCode();

        using var json = await TestClient.ReadJsonAsync(response);
        return json.RootElement.GetProperty("id").GetGuid();
    }

    public static async Task<Guid> CreatePreparationAsync(
        HttpClient client, string token, string name, decimal yieldQuantity,
        params (Guid IngredientId, decimal Quantity, string Unit)[] items)
    {
        var response = await client.WithToken(token).PostAsJsonAsync("/api/v1/preparations", new
        {
            name,
            yieldQuantity,
            yieldUnit = "un",
            items = items.Select(i => new
            {
                type = "Ingredient",
                ingredientId = i.IngredientId,
                preparationId = (Guid?)null,
                quantity = i.Quantity,
                unit = i.Unit
            }).ToArray()
        });
        response.EnsureSuccessStatusCode();

        using var json = await TestClient.ReadJsonAsync(response);
        return json.RootElement.GetProperty("id").GetGuid();
    }

    public static async Task CreateSheetAsync(
        HttpClient client, string token, Guid productId, Guid sizeId,
        params (Guid IngredientId, decimal Quantity, string Unit)[] items)
    {
        var response = await client.WithToken(token).PostAsJsonAsync("/api/v1/technicalsheets", new
        {
            productId,
            sizeId,
            packagingCost = 0m,
            lossPercentage = (decimal?)null,
            marginPercentage = (decimal?)null,
            items = items.Select(i => new
            {
                type = "Ingredient",
                ingredientId = i.IngredientId,
                preparationId = (Guid?)null,
                quantity = i.Quantity,
                unit = i.Unit
            }).ToArray()
        });
        response.EnsureSuccessStatusCode();
    }

    public static async Task<decimal> GetBalanceAsync(HttpClient client, string token, Guid ingredientId)
    {
        var response = await client.WithToken(token).GetAsync("/api/v1/stock/balances?pageSize=100");
        response.EnsureSuccessStatusCode();

        using var json = await TestClient.ReadJsonAsync(response);
        foreach (var item in json.RootElement.GetProperty("items").EnumerateArray())
        {
            if (item.GetProperty("ingredientId").GetGuid() == ingredientId)
                return item.GetProperty("currentStock").GetDecimal();
        }

        return 0m;
    }

    public static async Task<List<JsonElement>> GetMovementsAsync(HttpClient client, string token)
    {
        var response = await client.WithToken(token).GetAsync("/api/v1/stock/movements?pageSize=100");
        response.EnsureSuccessStatusCode();

        using var json = await TestClient.ReadJsonAsync(response);
        return json.RootElement.GetProperty("items").EnumerateArray()
            .Select(e => e.Clone())
            .ToList();
    }

    public static async Task<(Guid Id, string Code)> CreateOrderAsync(
        HttpClient client, string token, Guid sizeId,
        params (Guid ProductId, decimal Share)[] flavors)
    {
        var response = await client.WithToken(token).PostAsJsonAsync("/api/v1/orders", new
        {
            type = "Counter",
            customerName = (string?)null,
            customerPhone = (string?)null,
            addressDistrict = (string?)null,
            addressStreet = (string?)null,
            addressNumber = (string?)null,
            addressComplement = (string?)null,
            notes = (string?)null,
            paymentMethod = "Cash",
            discount = 0m,
            items = new[]
            {
                new
                {
                    sizeId = (Guid?)sizeId,
                    comboId = (Guid?)null,
                    quantity = 1,
                    notes = (string?)null,
                    flavors = flavors
                        .Select(f => new { productId = f.ProductId, sharePercentage = f.Share })
                        .ToArray()
                }
            }
        });
        response.EnsureSuccessStatusCode();

        using var json = await TestClient.ReadJsonAsync(response);
        var root = json.RootElement;
        return (root.GetProperty("id").GetGuid(), root.GetProperty("code").GetString()!);
    }

    public static async Task<(Guid Id, string Name, decimal Price)> CreateComboAsync(
        HttpClient client, string token, string name, decimal? fixedPrice, decimal discountValue,
        params (Guid ProductId, Guid? SizeId, int Quantity)[] items)
    {
        var response = await client.WithToken(token).PostAsJsonAsync("/api/v1/combos", new
        {
            name,
            discountValue,
            fixedPrice,
            items = items.Select(i => new
            {
                productId = i.ProductId,
                sizeId = i.SizeId,
                quantity = i.Quantity
            }).ToArray()
        });
        response.EnsureSuccessStatusCode();

        using var json = await TestClient.ReadJsonAsync(response);
        var root = json.RootElement;
        return (
            root.GetProperty("id").GetGuid(),
            root.GetProperty("name").GetString()!,
            root.GetProperty("totalPrice").GetDecimal()
        );
    }
}

using System.Net.Http.Json;

using static PizzariaLucrativa.Api.Tests.OrderFactory;

namespace PizzariaLucrativa.Api.Tests;

/// <summary>
/// Docs/10, Sprint 2: fluxo de pedido ponta a ponta (criar → start → ready → deliver),
/// baixa/estorno de estoque, máquina de status e isolamento por tenant.
/// </summary>
public sealed class OrderFlowTests
{
    [Fact]
    public async Task FullFlow_CreateStartReadyDeliver_ConsumesStockOnce()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var owner = await TestClient.RegisterAsync(client);
        var token = owner.AccessToken;

        var muçarela = await CreateIngredientAsync(client, token, "Muçarela", "g", 5000, 32.90m);
        var calabresa = await CreateIngredientAsync(client, token, "Calabresa", "g", 1000, 9.50m);
        await AddInitialStockAsync(client, token, muçarela, 5000);
        await AddInitialStockAsync(client, token, calabresa, 1000);

        var size = await CreateSizeAsync(client, token, "Grande");
        var pizza = await CreatePizzaAsync(client, token, "Calabresa");
        await CreateSheetAsync(client, token, pizza, size,
            (muçarela, 180m, "g"), (calabresa, 100m, "g"));

        var (orderId, code) = await CreateOrderAsync(client, token, size, (pizza, 100m));

        // Recebido: o estoque ainda está intacto (a baixa ocorre só em "Pronto" — P3).
        Assert.Equal(5000m, await GetBalanceAsync(client, token, muçarela));
        Assert.Equal(1000m, await GetBalanceAsync(client, token, calabresa));

        var start = await client.WithToken(token)
            .PostAsync($"/api/v1/kitchen/queue/{orderId}/start", null);
        start.EnsureSuccessStatusCode();

        var ready = await client.WithToken(token)
            .PostAsync($"/api/v1/kitchen/queue/{orderId}/ready", null);
        ready.EnsureSuccessStatusCode();

        // Baixa exatamente pela ficha técnica.
        Assert.Equal(4820m, await GetBalanceAsync(client, token, muçarela));
        Assert.Equal(900m, await GetBalanceAsync(client, token, calabresa));

        // Idempotência: repetir "Pronto" não baixa de novo.
        var readyAgain = await client.WithToken(token)
            .PostAsync($"/api/v1/kitchen/queue/{orderId}/ready", null);
        readyAgain.EnsureSuccessStatusCode();
        Assert.Equal(4820m, await GetBalanceAsync(client, token, muçarela));
        Assert.Equal(900m, await GetBalanceAsync(client, token, calabresa));

        // Um movimento OrderOut por ingrediente, referenciando o código do pedido.
        var movements = await GetMovementsAsync(client, token);
        Assert.Equal(2, movements.Count(m =>
            m.GetProperty("type").GetString() == "OrderOut"
            && (m.GetProperty("note").GetString() ?? string.Empty).Contains(code)));

        // Balcão: Pronto → Concluído (entra no faturamento).
        var complete = await client.WithToken(token)
            .PostAsJsonAsync($"/api/v1/orders/{orderId}/status", new { status = "Completed" });
        complete.EnsureSuccessStatusCode();

        using var completeJson = await TestClient.ReadJsonAsync(complete);
        Assert.Equal("Completed", completeJson.RootElement.GetProperty("status").GetString());
        Assert.Equal(4820m, await GetBalanceAsync(client, token, muçarela));
    }

    [Fact]
    public async Task CancelAfterReady_ReversesStock()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var owner = await TestClient.RegisterAsync(client);
        var token = owner.AccessToken;

        var muçarela = await CreateIngredientAsync(client, token, "Muçarela", "g", 5000, 32.90m);
        await AddInitialStockAsync(client, token, muçarela, 5000);

        var size = await CreateSizeAsync(client, token, "Grande");
        var pizza = await CreatePizzaAsync(client, token, "Muçarela");
        await CreateSheetAsync(client, token, pizza, size, (muçarela, 180m, "g"));

        var (orderId, code) = await CreateOrderAsync(client, token, size, (pizza, 100m));

        await client.WithToken(token)
            .PostAsync($"/api/v1/kitchen/queue/{orderId}/start", null);
        await client.WithToken(token)
            .PostAsync($"/api/v1/kitchen/queue/{orderId}/ready", null);

        Assert.Equal(4820m, await GetBalanceAsync(client, token, muçarela));

        var cancel = await client.WithToken(token)
            .PostAsJsonAsync($"/api/v1/orders/{orderId}/cancel", new { reason = "Cliente desistiu" });
        cancel.EnsureSuccessStatusCode();

        using var cancelJson = await TestClient.ReadJsonAsync(cancel);
        Assert.Equal("Cancelled", cancelJson.RootElement.GetProperty("status").GetString());

        // Estorno devolve o saldo exatamente ao valor inicial.
        Assert.Equal(5000m, await GetBalanceAsync(client, token, muçarela));

        var movements = await GetMovementsAsync(client, token);
        Assert.Equal(1, movements.Count(m =>
            m.GetProperty("type").GetString() == "In"
            && (m.GetProperty("note").GetString() ?? string.Empty)
                .Contains($"Estorno do pedido #{code}")));
    }

    [Fact]
    public async Task HalfHalf_ConsumesFractionOfEachSheet()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var owner = await TestClient.RegisterAsync(client);
        var token = owner.AccessToken;

        var muçarela = await CreateIngredientAsync(client, token, "Muçarela", "g", 5000, 32.90m);
        var calabresa = await CreateIngredientAsync(client, token, "Calabresa", "g", 1000, 9.50m);
        await AddInitialStockAsync(client, token, muçarela, 5000);
        await AddInitialStockAsync(client, token, calabresa, 1000);

        var size = await CreateSizeAsync(client, token, "Grande");
        var saborA = await CreatePizzaAsync(client, token, "Muçarela");
        var saborB = await CreatePizzaAsync(client, token, "Calabresa");
        await CreateSheetAsync(client, token, saborA, size, (muçarela, 180m, "g"));
        await CreateSheetAsync(client, token, saborB, size, (calabresa, 100m, "g"));

        // Meio a meio: cada sabor entra com 50%.
        var (orderId, _) = await CreateOrderAsync(client, token, size, (saborA, 50m), (saborB, 50m));

        await client.WithToken(token)
            .PostAsync($"/api/v1/kitchen/queue/{orderId}/start", null);
        await client.WithToken(token)
            .PostAsync($"/api/v1/kitchen/queue/{orderId}/ready", null);

        Assert.Equal(4910m, await GetBalanceAsync(client, token, muçarela)); // 5000 − 90
        Assert.Equal(950m, await GetBalanceAsync(client, token, calabresa)); // 1000 − 50
    }

    [Fact]
    public async Task IllegalTransition_Returns400()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var owner = await TestClient.RegisterAsync(client);
        var token = owner.AccessToken;

        var muçarela = await CreateIngredientAsync(client, token, "Muçarela", "g", 5000, 32.90m);

        var size = await CreateSizeAsync(client, token, "Grande");
        var pizza = await CreatePizzaAsync(client, token, "Calabresa");
        await CreateSheetAsync(client, token, pizza, size, (muçarela, 180m, "g"));

        var (orderId, _) = await CreateOrderAsync(client, token, size, (pizza, 100m));

        // Recebido → Concluído direto não é permitido (precisa passar pela cozinha).
        var response = await client.WithToken(token)
            .PostAsJsonAsync($"/api/v1/orders/{orderId}/status", new { status = "Completed" });
        await TestClient.AssertErrorEnvelopeAsync(response, 400);

        // Cancelamento sem motivo também é rejeitado.
        var noReason = await client.WithToken(token)
            .PostAsJsonAsync($"/api/v1/orders/{orderId}/cancel", new { reason = (string?)null });
        await TestClient.AssertErrorEnvelopeAsync(noReason, 400);
    }

    [Fact]
    public async Task OrderFromAnotherTenant_Returns404()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var tenantA = await TestClient.RegisterAsync(client, "Pizzaria A");
        var tokenA = tenantA.AccessToken;

        var muçarela = await CreateIngredientAsync(client, tokenA, "Muçarela", "g", 5000, 32.90m);

        var size = await CreateSizeAsync(client, tokenA, "Grande");
        var pizza = await CreatePizzaAsync(client, tokenA, "Calabresa");
        await CreateSheetAsync(client, tokenA, pizza, size, (muçarela, 180m, "g"));

        var (orderId, _) = await CreateOrderAsync(client, tokenA, size, (pizza, 100m));

        var tenantB = await TestClient.RegisterAsync(client, "Pizzaria B");
        var tokenB = tenantB.AccessToken;

        var getB = await client.WithToken(tokenB).GetAsync($"/api/v1/orders/{orderId}");
        await TestClient.AssertErrorEnvelopeAsync(getB, 404);

        var startB = await client.WithToken(tokenB)
            .PostAsync($"/api/v1/kitchen/queue/{orderId}/start", null);
        await TestClient.AssertErrorEnvelopeAsync(startB, 404);
    }
}

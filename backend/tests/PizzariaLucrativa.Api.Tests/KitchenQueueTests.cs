using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PizzariaLucrativa.Infrastructure.Persistence;

using static PizzariaLucrativa.Api.Tests.OrderFactory;

namespace PizzariaLucrativa.Api.Tests;

/// <summary>
/// Docs/10, Sprint 3: fila da produção (<c>/api/v1/kitchen/queue</c>), controle de
/// acesso por papel (<c>pizzaiolo</c> permitido, <c>atendente</c> 403), enriquecimento
/// de sabores/frações/receita, flag <c>late</c> (≥20 min) e ocultação de custos.
/// </summary>
public sealed class KitchenQueueTests
{
    private static async Task<(string Email, string Token)> CreateAndLoginUserAsync(
        HttpClient client, string ownerToken, string role, string fullName)
    {
        var email = $"{role}-{Guid.NewGuid():N}@teste.com";
        var createRes = await client
            .WithToken(ownerToken)
            .PostAsJsonAsync("/api/v1/users", new
            {
                email,
                password = "Senha123",
                fullName,
                role
            });
        createRes.EnsureSuccessStatusCode();

        var loginRes = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = "Senha123"
        });
        loginRes.EnsureSuccessStatusCode();

        using var json = await TestClient.ReadJsonAsync(loginRes);
        return (email, json.RootElement.GetProperty("accessToken").GetString()!);
    }

    [Fact]
    public async Task Pizzaiolo_CanAccessQueueAndAdvanceOrder_WhileAtendenteGets403()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var owner = await TestClient.RegisterAsync(client);
        var ownerToken = owner.AccessToken;

        var (_, pizzaioloToken) = await CreateAndLoginUserAsync(
            client, ownerToken, "pizzaiolo", "Pedro Pizzaiolo");
        var (_, atendenteToken) = await CreateAndLoginUserAsync(
            client, ownerToken, "atendente", "Ana Atendente");

        var mucarela = await CreateIngredientAsync(client, ownerToken, "Muçarela", "g", 5000, 32.90m);
        var calabresa = await CreateIngredientAsync(client, ownerToken, "Calabresa", "g", 1000, 9.50m);
        await AddInitialStockAsync(client, ownerToken, mucarela, 5000);
        await AddInitialStockAsync(client, ownerToken, calabresa, 1000);

        var size = await CreateSizeAsync(client, ownerToken, "Grande");
        var saborA = await CreatePizzaAsync(client, ownerToken, "Muçarela");
        var saborB = await CreatePizzaAsync(client, ownerToken, "Calabresa");
        await CreateSheetAsync(client, ownerToken, saborA, size, (mucarela, 180m, "g"));
        await CreateSheetAsync(client, ownerToken, saborB, size, (calabresa, 100m, "g"));

        var (orderId, code) = await CreateOrderAsync(
            client, ownerToken, size, (saborA, 50m), (saborB, 50m));

        // 1. Atendente não pode acessar a fila nem mover status da cozinha (403).
        var atendenteQueue = await client.WithToken(atendenteToken).GetAsync("/api/v1/kitchen/queue");
        await TestClient.AssertErrorEnvelopeAsync(atendenteQueue, 403);

        var atendenteStart = await client.WithToken(atendenteToken)
            .PostAsync($"/api/v1/kitchen/queue/{orderId}/start", null);
        await TestClient.AssertErrorEnvelopeAsync(atendenteStart, 403);

        var atendenteReady = await client.WithToken(atendenteToken)
            .PostAsync($"/api/v1/kitchen/queue/{orderId}/ready", null);
        await TestClient.AssertErrorEnvelopeAsync(atendenteReady, 403);

        // 2. Pizzaiolo lê a fila enriquecida (contagens, itens, sabores, frações e receita).
        var queueRes = await client.WithToken(pizzaioloToken).GetAsync("/api/v1/kitchen/queue");
        queueRes.EnsureSuccessStatusCode();

        using (var queueJson = await TestClient.ReadJsonAsync(queueRes))
        {
            var root = queueJson.RootElement;
            var counts = root.GetProperty("counts");
            Assert.Equal(1, counts.GetProperty("received").GetInt32());
            Assert.Equal(0, counts.GetProperty("inPreparation").GetInt32());
            Assert.Equal(0, counts.GetProperty("ready").GetInt32());
            Assert.Equal(1, counts.GetProperty("total").GetInt32());

            var card = root.GetProperty("items")[0];
            Assert.Equal(code, card.GetProperty("code").GetString());
            Assert.Equal("Received", card.GetProperty("status").GetString());
            Assert.Equal("Counter", card.GetProperty("type").GetString());
            Assert.False(card.GetProperty("late").GetBoolean());

            var item = card.GetProperty("items")[0];
            Assert.Equal(1, item.GetProperty("quantity").GetInt32());
            Assert.Equal("Grande", item.GetProperty("sizeName").GetString());

            var flavors = item.GetProperty("flavors");
            Assert.Equal(2, flavors.GetArrayLength());
            Assert.Equal(50m, flavors[0].GetProperty("sharePercentage").GetDecimal());
            Assert.True(flavors[0].GetProperty("recipe").GetArrayLength() >= 1);
        }

        // 3. Pizzaiolo move para "Em preparo" (start) e depois "Pronto" (ready).
        var startRes = await client.WithToken(pizzaioloToken)
            .PostAsync($"/api/v1/kitchen/queue/{orderId}/start", null);
        startRes.EnsureSuccessStatusCode();
        using (var startJson = await TestClient.ReadJsonAsync(startRes))
        {
            Assert.Equal("InPreparation", startJson.RootElement.GetProperty("status").GetString());
            Assert.NotEqual(
                System.Text.Json.JsonValueKind.Null,
                startJson.RootElement.GetProperty("startedAt").ValueKind);
        }

        var readyRes = await client.WithToken(pizzaioloToken)
            .PostAsync($"/api/v1/kitchen/queue/{orderId}/ready", null);
        readyRes.EnsureSuccessStatusCode();
        using (var readyJson = await TestClient.ReadJsonAsync(readyRes))
        {
            Assert.Equal("Ready", readyJson.RootElement.GetProperty("status").GetString());
        }

        // Estoque baixado na fração 50/50.
        Assert.Equal(4910m, await GetBalanceAsync(client, ownerToken, mucarela));
        Assert.Equal(950m, await GetBalanceAsync(client, ownerToken, calabresa));

        // 4. Ao despachar/concluir ("finish"), o pedido sai da fila ativa da cozinha.
        var finishRes = await client.WithToken(pizzaioloToken)
            .PostAsync($"/api/v1/kitchen/queue/{orderId}/finish", null);
        finishRes.EnsureSuccessStatusCode();

        var afterFinishQueue = await client.WithToken(pizzaioloToken).GetAsync("/api/v1/kitchen/queue");
        afterFinishQueue.EnsureSuccessStatusCode();
        using (var afterJson = await TestClient.ReadJsonAsync(afterFinishQueue))
        {
            Assert.Equal(0, afterJson.RootElement.GetProperty("counts").GetProperty("total").GetInt32());
            Assert.Equal(0, afterJson.RootElement.GetProperty("items").GetArrayLength());
        }
    }

    [Fact]
    public async Task Queue_FlagsLateOrderAfter20Minutes_AndNeverExposesCostOrMargin()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var owner = await TestClient.RegisterAsync(client);
        var ownerToken = owner.AccessToken;

        var mucarela = await CreateIngredientAsync(client, ownerToken, "Muçarela", "g", 5000, 32.90m);
        var size = await CreateSizeAsync(client, ownerToken, "Grande");
        var pizza = await CreatePizzaAsync(client, ownerToken, "Muçarela");
        await CreateSheetAsync(client, ownerToken, pizza, size, (mucarela, 180m, "g"));

        var (orderId, _) = await CreateOrderAsync(client, ownerToken, size, (pizza, 100m));

        // Retrocede PlacedAt em 25 min para exercitar o cálculo de atraso (≥20 min).
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var order = await db.Orders.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.Id == orderId);
            Assert.NotNull(order);
            order!.PlacedAt = DateTime.UtcNow.AddMinutes(-25);
            await db.SaveChangesAsync();
        }

        var queueRes = await client.WithToken(ownerToken).GetAsync("/api/v1/kitchen/queue");
        queueRes.EnsureSuccessStatusCode();

        var rawBody = await queueRes.Content.ReadAsStringAsync();
        Assert.DoesNotContain("cost", rawBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("margin", rawBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("profit", rawBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("unitPrice", rawBody, StringComparison.OrdinalIgnoreCase);

        using var json = await TestClient.ReadJsonAsync(queueRes);
        var card = json.RootElement.GetProperty("items")[0];
        Assert.True(card.GetProperty("elapsedMinutes").GetInt32() >= 20);
        Assert.True(card.GetProperty("late").GetBoolean());
    }
}

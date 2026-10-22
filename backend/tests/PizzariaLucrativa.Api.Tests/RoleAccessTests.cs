using System.Net;
using System.Net.Http.Json;

namespace PizzariaLucrativa.Api.Tests;

/// <summary>
/// Docs/10, Sprint 1: papéis (owner/atendente/pizzaiolo), gestão de usuários
/// e proteção do último owner.
/// </summary>
public sealed class RoleAccessTests
{
    private static async Task<(HttpClient Client, string Token)> LoginAsAsync(
        HttpClient client, string email, string password = "Senha123")
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password
        });
        response.EnsureSuccessStatusCode();

        using var json = await TestClient.ReadJsonAsync(response);
        return (client, json.RootElement.GetProperty("accessToken").GetString()!);
    }

    private static async Task<(string Email, string Id)> CreateUserAsync(
        HttpClient client, string ownerToken, string role, string fullName)
    {
        var email = $"{role}-{Guid.NewGuid():N}@teste.com";
        var response = await client
            .WithToken(ownerToken)
            .PostAsJsonAsync("/api/v1/users", new
            {
                email,
                password = "Senha123",
                fullName,
                role
            });

        response.EnsureSuccessStatusCode();

        using var json = await TestClient.ReadJsonAsync(response);
        return (email, json.RootElement.GetProperty("id").GetString()!);
    }

    [Fact]
    public async Task Atendente_CannotAccessDashboard_ButOwnerCan()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var owner = await TestClient.RegisterAsync(client);
        var (atendenteEmail, _) = await CreateUserAsync(client, owner.AccessToken, "atendente", "Ana Atendente");
        var (_, atendenteToken) = await LoginAsAsync(client, atendenteEmail);

        // Atendente: 403 (autenticado, mas sem papel).
        var forbidden = await client
            .WithToken(atendenteToken)
            .GetAsync("/api/v1/dashboard/summary");
        await TestClient.AssertErrorEnvelopeAsync(forbidden, 403);

        // Owner: 200.
        var allowed = await client
            .WithToken(owner.AccessToken)
            .GetAsync("/api/v1/dashboard/summary");
        allowed.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Pizzaiolo_CannotManageUsers_ButOwnerCan()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var owner = await TestClient.RegisterAsync(client);
        var (pizzaioloEmail, _) = await CreateUserAsync(client, owner.AccessToken, "pizzaiolo", "Pedro Pizzaiolo");
        var (_, pizzaioloToken) = await LoginAsAsync(client, pizzaioloEmail);

        var forbidden = await client
            .WithToken(pizzaioloToken)
            .GetAsync("/api/v1/users");
        await TestClient.AssertErrorEnvelopeAsync(forbidden, 403);

        var allowed = await client
            .WithToken(owner.AccessToken)
            .GetAsync("/api/v1/users");
        allowed.EnsureSuccessStatusCode();

        using var json = await TestClient.ReadJsonAsync(allowed);
        // Dono + pizzaiolo.
        Assert.Equal(2, json.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task Owner_CreatesUsers_AndTheySeeTheirOwnRole()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var owner = await TestClient.RegisterAsync(client);
        var (atendenteEmail, _) = await CreateUserAsync(client, owner.AccessToken, "atendente", "Ana Atendente");
        var (pizzaioloEmail, _) = await CreateUserAsync(client, owner.AccessToken, "pizzaiolo", "Pedro Pizzaiolo");

        var (_, atendenteToken) = await LoginAsAsync(client, atendenteEmail);
        var (_, pizzaioloToken) = await LoginAsAsync(client, pizzaioloEmail);

        // Atendente acessa o estoque (permitido ao papel), mas não a precificação.
        var stock = await client
            .WithToken(atendenteToken)
            .GetAsync("/api/v1/stock/balances");
        Assert.NotEqual(HttpStatusCode.Forbidden, stock.StatusCode);

        var pricing = await client
            .WithToken(atendenteToken)
            .PostAsJsonAsync("/api/v1/pricing/preview", new { });
        await TestClient.AssertErrorEnvelopeAsync(pricing, 403);

        // Pizzaiolo: criação de usuário negada, mas fila de produção liberada
        // (o endpoint de produção nasce na Sprint 3 — aqui validamos a política).
        var users = await client
            .WithToken(pizzaioloToken)
            .PostAsJsonAsync("/api/v1/users", new
            {
                email = $"x-{Guid.NewGuid():N}@teste.com",
                password = "Senha123",
                fullName = "X",
                role = "atendente"
            });
        await TestClient.AssertErrorEnvelopeAsync(users, 403);
    }

    [Fact]
    public async Task LastOwner_CannotBeRemovedOrDemoted()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var owner = await TestClient.RegisterAsync(client);

        // Lista o próprio id.
        var list = await client
            .WithToken(owner.AccessToken)
            .GetAsync("/api/v1/users");
        list.EnsureSuccessStatusCode();

        using var listJson = await TestClient.ReadJsonAsync(list);
        var ownerId = listJson.RootElement[0].GetProperty("id").GetString()!;

        // Não pode ser removido.
        var delete = await client
            .WithToken(owner.AccessToken)
            .DeleteAsync($"/api/v1/users/{ownerId}");
        await TestClient.AssertErrorEnvelopeAsync(delete, 409);

        // Não pode ser rebaixado de owner.
        var demote = await client
            .WithToken(owner.AccessToken)
            .PutAsJsonAsync($"/api/v1/users/{ownerId}", new
            {
                fullName = "Dono",
                role = "atendente"
            });
        await TestClient.AssertErrorEnvelopeAsync(demote, 409);

        // Com um segundo owner, o primeiro pode sair.
        var secondOwnerEmail = $"dono2-{Guid.NewGuid():N}@teste.com";
        var created = await client
            .WithToken(owner.AccessToken)
            .PostAsJsonAsync("/api/v1/users", new
            {
                email = secondOwnerEmail,
                password = "Senha123",
                fullName = "Segundo Dono",
                role = "owner"
            });
        created.EnsureSuccessStatusCode();

        var deleteAfter = await client
            .WithToken(owner.AccessToken)
            .DeleteAsync($"/api/v1/users/{ownerId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteAfter.StatusCode);
    }

    [Fact]
    public async Task User_FromAnotherTenant_Returns404()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var tenantA = await TestClient.RegisterAsync(client, "Pizzaria A");
        var (_, userIdOfA) = await CreateUserAsync(client, tenantA.AccessToken, "atendente", "Ana A");

        var tenantB = await TestClient.RegisterAsync(client, "Pizzaria B");

        var getB = await client
            .WithToken(tenantB.AccessToken)
            .PutAsJsonAsync($"/api/v1/users/{userIdOfA}", new { fullName = "Hackeado", role = "owner" });
        await TestClient.AssertErrorEnvelopeAsync(getB, 404);

        var deleteB = await client
            .WithToken(tenantB.AccessToken)
            .DeleteAsync($"/api/v1/users/{userIdOfA}");
        await TestClient.AssertErrorEnvelopeAsync(deleteB, 404);
    }

    [Fact]
    public async Task Garcom_Permissions_CanCreateAndListOrders_CannotAccessReportsKitchenOrUsers()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var owner = await TestClient.RegisterAsync(client);
        var (garcomEmail, _) = await CreateUserAsync(client, owner.AccessToken, "garcom", "Gabriel Garçom");
        var (_, garcomToken) = await LoginAsAsync(client, garcomEmail);

        // Configuração mínima de catálogo pelo owner para permitir pedido
        var ingrediente = await OrderFactory.CreateIngredientAsync(client, owner.AccessToken, "Muçarela", "g", 1000, 30m);
        await OrderFactory.AddInitialStockAsync(client, owner.AccessToken, ingrediente, 1000);
        var size = await OrderFactory.CreateSizeAsync(client, owner.AccessToken, "Individual");
        var pizza = await OrderFactory.CreatePizzaAsync(client, owner.AccessToken, "Muçarela");
        await OrderFactory.CreateSheetAsync(client, owner.AccessToken, pizza, size, (ingrediente, 150m, "g"));

        // 1. O garçom pode efetuar login e criar pedidos via POST /api/v1/orders
        var (orderId, code) = await OrderFactory.CreateOrderAsync(client, garcomToken, size, (pizza, 100m));
        Assert.NotEqual(Guid.Empty, orderId);
        Assert.False(string.IsNullOrWhiteSpace(code));

        // 2. O garçom pode listar pedidos em GET /api/v1/orders/today ou GET /api/v1/orders
        var today = await client
            .WithToken(garcomToken)
            .GetAsync("/api/v1/orders/today");
        today.EnsureSuccessStatusCode();

        var orders = await client
            .WithToken(garcomToken)
            .GetAsync("/api/v1/orders");
        orders.EnsureSuccessStatusCode();

        // 3. O garçom NÃO pode acessar relatórios de lucro GET /api/v1/reports/profit (403 Forbidden)
        var profit = await client
            .WithToken(garcomToken)
            .GetAsync("/api/v1/reports/profit");
        await TestClient.AssertErrorEnvelopeAsync(profit, 403);

        // 4. O garçom NÃO pode acessar a fila da cozinha GET /api/v1/kitchen/queue (403 Forbidden)
        var kitchen = await client
            .WithToken(garcomToken)
            .GetAsync("/api/v1/kitchen/queue");
        await TestClient.AssertErrorEnvelopeAsync(kitchen, 403);

        // 5. O garçom NÃO pode gerenciar usuários GET /api/v1/users (403 Forbidden)
        var users = await client
            .WithToken(garcomToken)
            .GetAsync("/api/v1/users");
        await TestClient.AssertErrorEnvelopeAsync(users, 403);
    }
}

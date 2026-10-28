using System.Net;
using System.Net.Http.Json;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Domain.Enums;

namespace PizzariaLucrativa.Api.Tests;

public sealed class StockReplenishmentApiTests
{
    [Fact]
    public async Task ReplenishmentReport_ShouldRequireAuthentication()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/stock/replenishment-report");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ReplenishmentReport_ShouldReturnReport_WithFilteringAndUrgencySorting()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var auth = await TestClient.RegisterAsync(client, "Pizzaria Estoque");
        var authClient = client.WithToken(auth.AccessToken);

        // 1. Insumo Crítico (Estoque 0 <= 0, Min 1000g)
        var critRes = await authClient.PostAsJsonAsync("/api/v1/ingredients", new
        {
            name = "Queijo Gorgonzola",
            purchaseUnit = MeasureUnit.g,
            purchaseQuantity = 1000m,
            purchasePrice = 50m,
            lossPercentage = 0m,
            minimumStock = 1000m
        });
        critRes.EnsureSuccessStatusCode();

        // 2. Insumo Baixo (Estoque 500g <= Min 1000g)
        var lowRes = await authClient.PostAsJsonAsync("/api/v1/ingredients", new
        {
            name = "Bacon Fatiado",
            purchaseUnit = MeasureUnit.g,
            purchaseQuantity = 1000m,
            purchasePrice = 40m,
            lossPercentage = 0m,
            minimumStock = 1000m
        });
        lowRes.EnsureSuccessStatusCode();
        using (var lowDoc = await TestClient.ReadJsonAsync(lowRes))
        {
            var lowId = Guid.Parse(lowDoc.RootElement.GetProperty("id").GetString()!);
            // Dar entrada de 500g
            var moveRes = await authClient.PostAsJsonAsync("/api/v1/stock/movements", new
            {
                ingredientId = lowId,
                type = StockMovementType.In,
                quantity = 500m,
                unitCost = 0.04m
            });
            moveRes.EnsureSuccessStatusCode();
        }

        // 3. Insumo Normal (Estoque 2000g > Min 1000g * 1.25)
        var normRes = await authClient.PostAsJsonAsync("/api/v1/ingredients", new
        {
            name = "Orégano Seco",
            purchaseUnit = MeasureUnit.g,
            purchaseQuantity = 500m,
            purchasePrice = 15m,
            lossPercentage = 0m,
            minimumStock = 1000m
        });
        normRes.EnsureSuccessStatusCode();
        using (var normDoc = await TestClient.ReadJsonAsync(normRes))
        {
            var normId = Guid.Parse(normDoc.RootElement.GetProperty("id").GetString()!);
            var moveRes = await authClient.PostAsJsonAsync("/api/v1/stock/movements", new
            {
                ingredientId = normId,
                type = StockMovementType.In,
                quantity = 2000m,
                unitCost = 0.03m
            });
            moveRes.EnsureSuccessStatusCode();
        }

        var jsonOptions = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)
        {
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

        // 4. Testar Endpoint Padrão (onlyNeedingReplenishment = default true)
        var reportDefault = await authClient.GetFromJsonAsync<StockReplenishmentReportDto>(
            "/api/v1/stock/replenishment-report", jsonOptions);
        Assert.NotNull(reportDefault);
        Assert.Equal(2, reportDefault.TotalItemsNeedingReplenishment);
        Assert.Equal(1, reportDefault.CriticalCount);
        Assert.Equal(1, reportDefault.LowCount);
        Assert.Equal(0, reportDefault.WarningCount);
        Assert.Equal(2, reportDefault.Items.Count);

        // Deve vir Gorgonzola (Critical) primeiro, depois Bacon (Low)
        Assert.Equal("Queijo Gorgonzola", reportDefault.Items[0].IngredientName);
        Assert.Equal(StockReplenishmentUrgency.Critical, reportDefault.Items[0].Urgency);
        Assert.Equal(1000m, reportDefault.Items[0].DeficitQuantity);
        Assert.Equal(1, reportDefault.Items[0].SuggestedPackagesToBuy);
        Assert.Equal(50m, reportDefault.Items[0].EstimatedCost);

        Assert.Equal("Bacon Fatiado", reportDefault.Items[1].IngredientName);
        Assert.Equal(StockReplenishmentUrgency.Low, reportDefault.Items[1].Urgency);
        Assert.Equal(500m, reportDefault.Items[1].DeficitQuantity);
        Assert.Equal(1, reportDefault.Items[1].SuggestedPackagesToBuy);
        Assert.Equal(40m, reportDefault.Items[1].EstimatedCost);

        // Custo total = 50 + 40 = 90
        Assert.Equal(90m, reportDefault.TotalEstimatedCost);

        // 5. Testar trazendo todos (onlyNeedingReplenishment=false)
        var reportAll = await authClient.GetFromJsonAsync<StockReplenishmentReportDto>(
            "/api/v1/stock/replenishment-report?onlyNeedingReplenishment=false", jsonOptions);
        Assert.NotNull(reportAll);
        Assert.Equal(3, reportAll.Items.Count);

        // 6. Testar filtro por Urgency=Critical
        var reportCritOnly = await authClient.GetFromJsonAsync<StockReplenishmentReportDto>(
            "/api/v1/stock/replenishment-report?urgency=Critical", jsonOptions);
        Assert.NotNull(reportCritOnly);
        Assert.Single(reportCritOnly.Items);
        Assert.Equal("Queijo Gorgonzola", reportCritOnly.Items[0].IngredientName);

        // 7. Testar filtro por busca textual
        var reportSearch = await authClient.GetFromJsonAsync<StockReplenishmentReportDto>(
            "/api/v1/stock/replenishment-report?search=Bacon", jsonOptions);
        Assert.NotNull(reportSearch);
        Assert.Single(reportSearch.Items);
        Assert.Equal("Bacon Fatiado", reportSearch.Items[0].IngredientName);
    }

    [Fact]
    public async Task ReplenishmentReport_ShouldEnforceStrictTenantIsolation_BetweenTenants()
    {
        using var factory = new ApiFactory();
        using var clientA = factory.CreateClient();
        using var clientB = factory.CreateClient();

        var jsonOptions = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)
        {
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

        // Tenant A cria insumo crítico "Farinha Tenant A"
        var tenantA = await TestClient.RegisterAsync(clientA, "Pizzaria Alpha");
        clientA.WithToken(tenantA.AccessToken);

        var resA = await clientA.PostAsJsonAsync("/api/v1/ingredients", new
        {
            name = "Farinha Tenant A",
            purchaseUnit = MeasureUnit.kg,
            purchaseQuantity = 25m,
            purchasePrice = 120m,
            lossPercentage = 0m,
            minimumStock = 50m
        });
        resA.EnsureSuccessStatusCode();

        // Tenant B cria insumo crítico "Molho Tenant B"
        var tenantB = await TestClient.RegisterAsync(clientB, "Pizzaria Beta");
        clientB.WithToken(tenantB.AccessToken);

        var resB = await clientB.PostAsJsonAsync("/api/v1/ingredients", new
        {
            name = "Molho Tenant B",
            purchaseUnit = MeasureUnit.un,
            purchaseQuantity = 10m,
            purchasePrice = 75m,
            lossPercentage = 0m,
            minimumStock = 20m
        });
        resB.EnsureSuccessStatusCode();

        // 1. Tenant A deve ver somente o seu insumo
        var reportA = await clientA.GetFromJsonAsync<StockReplenishmentReportDto>(
            "/api/v1/stock/replenishment-report", jsonOptions);

        Assert.NotNull(reportA);
        Assert.Equal(1, reportA.TotalItemsNeedingReplenishment);
        Assert.Single(reportA.Items);
        Assert.Equal("Farinha Tenant A", reportA.Items[0].IngredientName);
        Assert.DoesNotContain(reportA.Items, i => i.IngredientName == "Molho Tenant B");

        // 2. Tenant B deve ver somente o seu insumo
        var reportB = await clientB.GetFromJsonAsync<StockReplenishmentReportDto>(
            "/api/v1/stock/replenishment-report", jsonOptions);

        Assert.NotNull(reportB);
        Assert.Equal(1, reportB.TotalItemsNeedingReplenishment);
        Assert.Single(reportB.Items);
        Assert.Equal("Molho Tenant B", reportB.Items[0].IngredientName);
        Assert.DoesNotContain(reportB.Items, i => i.IngredientName == "Farinha Tenant A");
    }

    [Fact]
    public async Task ReplenishmentReport_ShouldBeAccessibleByAuthenticatedRoles_SuchAsAtendente()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var owner = await TestClient.RegisterAsync(client, "Pizzaria com Equipe");

        // Cria atendente
        var atendenteEmail = $"atendente-{Guid.NewGuid():N}@teste.com";
        var createRes = await client
            .WithToken(owner.AccessToken)
            .PostAsJsonAsync("/api/v1/users", new
            {
                email = atendenteEmail,
                password = "Senha123",
                fullName = "Atendente da Silva",
                role = "atendente"
            });
        createRes.EnsureSuccessStatusCode();

        // Login como atendente
        var loginRes = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = atendenteEmail,
            password = "Senha123"
        });
        loginRes.EnsureSuccessStatusCode();

        using var loginDoc = await TestClient.ReadJsonAsync(loginRes);
        var atendenteToken = loginDoc.RootElement.GetProperty("accessToken").GetString()!;

        // Atendente acessa o replenishment report
        var reportRes = await client
            .WithToken(atendenteToken)
            .GetAsync("/api/v1/stock/replenishment-report");

        Assert.Equal(HttpStatusCode.OK, reportRes.StatusCode);
    }
}

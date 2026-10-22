using System.Net;
using System.Net.Http.Json;

namespace PizzariaLucrativa.Api.Tests;

/// <summary>NF-S03/NF-S04: nenhum dado de um tenant é visível para outro.</summary>
public sealed class TenantIsolationTests
{
    [Fact]
    public async Task TenantB_CannotSeeOrReadTenantA_Resources()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var tenantA = await TestClient.RegisterAsync(client, "Pizzaria A");
        var createdA = await client
            .WithToken(tenantA.AccessToken)
            .PostAsJsonAsync("/api/v1/ingredients", new
            {
                name = "Muçarela A",
                purchaseUnit = "kg",
                purchaseQuantity = 1m,
                purchasePrice = 40m,
                lossPercentage = 0m,
                minimumStock = 0m
            });
        createdA.EnsureSuccessStatusCode();

        using var createdJson = await TestClient.ReadJsonAsync(createdA);
        var ingredientAId = createdJson.RootElement.GetProperty("id").GetString();

        var tenantB = await TestClient.RegisterAsync(client, "Pizzaria B");

        // Lista de B não enxerga nada de A.
        var listB = await client
            .WithToken(tenantB.AccessToken)
            .GetAsync("/api/v1/ingredients");
        listB.EnsureSuccessStatusCode();

        using var listJson = await TestClient.ReadJsonAsync(listB);
        Assert.Equal(0, listJson.RootElement.GetProperty("total").GetInt32());
        Assert.Empty(listJson.RootElement.GetProperty("items").EnumerateArray());

        // Acesso direto ao id de A resulta em 404 (não revela existência).
        var getAFromB = await client
            .WithToken(tenantB.AccessToken)
            .GetAsync($"/api/v1/ingredients/{ingredientAId}");
        Assert.Equal(HttpStatusCode.NotFound, getAFromB.StatusCode);

        // B cria o próprio insumo; A continua sem vê-lo.
        var createdB = await client
            .WithToken(tenantB.AccessToken)
            .PostAsJsonAsync("/api/v1/ingredients", new
            {
                name = "Muçarela B",
                purchaseUnit = "kg",
                purchaseQuantity = 1m,
                purchasePrice = 50m,
                lossPercentage = 0m,
                minimumStock = 0m
            });
        createdB.EnsureSuccessStatusCode();

        using var createdBJson = await TestClient.ReadJsonAsync(createdB);
        var ingredientBId = createdBJson.RootElement.GetProperty("id").GetString();

        var getBFromA = await client
            .WithToken(tenantA.AccessToken)
            .GetAsync($"/api/v1/ingredients/{ingredientBId}");
        Assert.Equal(HttpStatusCode.NotFound, getBFromA.StatusCode);

        var listA = await client
            .WithToken(tenantA.AccessToken)
            .GetAsync("/api/v1/ingredients");
        listA.EnsureSuccessStatusCode();

        using var listAJson = await TestClient.ReadJsonAsync(listA);
        Assert.Equal(1, listAJson.RootElement.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task TenantB_HasItsOwnDefaultFees_NotTenantAValues()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var tenantA = await TestClient.RegisterAsync(client, "Pizzaria A");

        var updateA = await client
            .WithToken(tenantA.AccessToken)
            .PutAsJsonAsync("/api/v1/fees", new
            {
                cardFeePercentage = 3.5m,
                deliveryFeePercentage = 0m,
                deliveryFixedValue = 0m,
                defaultMarginPercentage = 60m,
                lossPercentageGlobal = 0m
            });
        updateA.EnsureSuccessStatusCode();

        var tenantB = await TestClient.RegisterAsync(client, "Pizzaria B");

        var feesB = await client
            .WithToken(tenantB.AccessToken)
            .GetAsync("/api/v1/fees");
        feesB.EnsureSuccessStatusCode();

        using var json = await TestClient.ReadJsonAsync(feesB);
        Assert.Equal(2.99m, json.RootElement.GetProperty("cardFeePercentage").GetDecimal());
        Assert.Equal(60m, json.RootElement.GetProperty("defaultMarginPercentage").GetDecimal());
    }
}

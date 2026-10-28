using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PizzariaLucrativa.Application.DTOs;

namespace PizzariaLucrativa.Api.Tests;

public sealed class BeveragesTests
{
    [Fact]
    public async Task UpdateMarkup_PersistsCurrentAndMinimumStock_AndRetainsValuesOnList()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var owner = await TestClient.RegisterAsync(client, "Pizzaria Bebidas");
        var token = owner.AccessToken;

        // 1. Cria uma bebida via POST /api/v1/products
        var createRes = await client.WithToken(token).PostAsJsonAsync("/api/v1/products", new
        {
            name = "Refrigerante Cola 2L",
            category = "Beverage",
            isActive = true,
            purchasePrice = 7.00m,
            markupMultiplier = 2.0m,
            currentStock = 10m,
            minimumStock = 3m
        });
        createRes.EnsureSuccessStatusCode();

        Guid beverageId;
        using (var createJson = await TestClient.ReadJsonAsync(createRes))
        {
            beverageId = createJson.RootElement.GetProperty("id").GetGuid();
        }

        // 2. Consulta GET /api/v1/beverages
        var listRes1 = await client.WithToken(token).GetAsync("/api/v1/beverages");
        listRes1.EnsureSuccessStatusCode();

        var jsonOptions = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

        var list1 = await listRes1.Content.ReadFromJsonAsync<List<BeverageDto>>(jsonOptions);
        Assert.NotNull(list1);
        var item1 = Assert.Single(list1, b => b.Id == beverageId);
        Assert.Equal(10m, item1.CurrentStock);
        Assert.Equal(3m, item1.MinimumStock);

        // 3. Atualiza PUT /api/v1/beverages/{id}/markup com novos valores de estoque
        var updateRes = await client.WithToken(token).PutAsJsonAsync($"/api/v1/beverages/{beverageId}/markup", new
        {
            markupMultiplier = 2.5m,
            purchasePrice = 8.00m,
            currentStock = 50m,
            minimumStock = 12m
        });
        updateRes.EnsureSuccessStatusCode();

        var updated = await updateRes.Content.ReadFromJsonAsync<BeverageDto>(jsonOptions);
        Assert.NotNull(updated);
        Assert.Equal(50m, updated.CurrentStock);
        Assert.Equal(12m, updated.MinimumStock);
        Assert.Equal(8.00m, updated.PurchasePrice);
        Assert.Equal(2.5m, updated.MarkupMultiplier);

        // 4. Nova chamada GET /api/v1/beverages — NÃO PODE VOLTAR PARA 0!
        var listRes2 = await client.WithToken(token).GetAsync("/api/v1/beverages");
        listRes2.EnsureSuccessStatusCode();

        var list2 = await listRes2.Content.ReadFromJsonAsync<List<BeverageDto>>(jsonOptions);
        Assert.NotNull(list2);
        var item2 = Assert.Single(list2, b => b.Id == beverageId);
        Assert.Equal(50m, item2.CurrentStock);
        Assert.Equal(12m, item2.MinimumStock);
    }

    [Fact]
    public async Task Beverage_WithoutIngredientId_WhenListed_ThenUpdatedWithStock_RetainsStock()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var owner = await TestClient.RegisterAsync(client, "Pizzaria Sem Insumo");
        var token = owner.AccessToken;

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var tenantId = Guid.Parse(jwt.Claims.First(c => c.Type == "tenant_id").Value);

        // Inserção direta de produto Beverage sem IngredientId no banco do tenant
        Guid beverageId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PizzariaLucrativa.Infrastructure.Persistence.AppDbContext>();
            var product = new PizzariaLucrativa.Domain.Entities.Product
            {
                TenantId = tenantId,
                Name = "Cerveja Lata",
                Category = PizzariaLucrativa.Domain.Enums.ProductCategory.Beverage,
                IsActive = true,
                PurchasePrice = 4.00m,
                MarkupMultiplier = 2.0m,
                IngredientId = null
            };
            db.Products.Add(product);
            await db.SaveChangesAsync();
            beverageId = product.Id;
        }

        var jsonOptions = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

        // 1. GET inicial - cria o insumo automaticamente
        var listRes1 = await client.WithToken(token).GetAsync("/api/v1/beverages");
        listRes1.EnsureSuccessStatusCode();

        var list1 = await listRes1.Content.ReadFromJsonAsync<List<BeverageDto>>(jsonOptions);
        Assert.NotNull(list1);
        var item1 = Assert.Single(list1, b => b.Id == beverageId);
        Assert.Equal(0m, item1.CurrentStock);

        // 2. PUT atualizando o estoque para 30 e 5
        var updateRes = await client.WithToken(token).PutAsJsonAsync($"/api/v1/beverages/{beverageId}/markup", new
        {
            markupMultiplier = 2.2m,
            purchasePrice = 4.50m,
            currentStock = 30m,
            minimumStock = 5m
        });
        updateRes.EnsureSuccessStatusCode();

        var updated = await updateRes.Content.ReadFromJsonAsync<BeverageDto>(jsonOptions);
        Assert.NotNull(updated);
        Assert.Equal(30m, updated.CurrentStock);
        Assert.Equal(5m, updated.MinimumStock);

        // 3. GET subsequente - NÃO PODE VOLTAR PARA 0!
        var listRes2 = await client.WithToken(token).GetAsync("/api/v1/beverages");
        listRes2.EnsureSuccessStatusCode();

        var list2 = await listRes2.Content.ReadFromJsonAsync<List<BeverageDto>>(jsonOptions);
        Assert.NotNull(list2);
        var item2 = Assert.Single(list2, b => b.Id == beverageId);
        Assert.Equal(30m, item2.CurrentStock);
        Assert.Equal(5m, item2.MinimumStock);
    }

    [Fact]
    public async Task Beverage_UpdateMarkup_WithNumericStrings_PersistsCorrectly()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var owner = await TestClient.RegisterAsync(client, "Pizzaria Strings");
        var token = owner.AccessToken;

        // Cria produto Bebida
        var createRes = await client.WithToken(token).PostAsJsonAsync("/api/v1/products", new
        {
            name = "Suco de Laranja 500ml",
            category = "Beverage",
            isActive = true,
            purchasePrice = 3.50m,
            markupMultiplier = 2.0m,
            currentStock = 0m,
            minimumStock = 0m
        });
        createRes.EnsureSuccessStatusCode();

        Guid beverageId;
        using (var createJson = await TestClient.ReadJsonAsync(createRes))
        {
            beverageId = createJson.RootElement.GetProperty("id").GetGuid();
        }

        // PUT enviando strings com aspas no JSON, simulando binding de formulário frontend
        var stringJson = "{\"markupMultiplier\":\"2.5\",\"purchasePrice\":\"4.00\",\"currentStock\":\"45\",\"minimumStock\":\"8\"}";
        var content = new StringContent(stringJson, System.Text.Encoding.UTF8, "application/json");

        var updateRes = await client.WithToken(token).PutAsync($"/api/v1/beverages/{beverageId}/markup", content);
        updateRes.EnsureSuccessStatusCode();

        var jsonOptions = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

        var updated = await updateRes.Content.ReadFromJsonAsync<BeverageDto>(jsonOptions);
        Assert.NotNull(updated);
        Assert.Equal(45m, updated.CurrentStock);
        Assert.Equal(8m, updated.MinimumStock);
        Assert.Equal(4.00m, updated.PurchasePrice);
        Assert.Equal(2.5m, updated.MarkupMultiplier);

        // GET confirma permanência dos dados
        var listRes = await client.WithToken(token).GetAsync("/api/v1/beverages");
        listRes.EnsureSuccessStatusCode();

        var list = await listRes.Content.ReadFromJsonAsync<List<BeverageDto>>(jsonOptions);
        Assert.NotNull(list);
        var item = Assert.Single(list, b => b.Id == beverageId);
        Assert.Equal(45m, item.CurrentStock);
        Assert.Equal(8m, item.MinimumStock);
    }
}

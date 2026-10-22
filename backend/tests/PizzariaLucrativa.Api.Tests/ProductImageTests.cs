using System.Net.Http.Json;
using static PizzariaLucrativa.Api.Tests.OrderFactory;

namespace PizzariaLucrativa.Api.Tests;

/// <summary>
/// Testes de integração para funcionalidade de imagens nos produtos:
/// - Criação e atualização de produtos via ProductService (trim, nulos, vazios e persistência).
/// - Propagação da ImageUrl para a fila da cozinha (KitchenService: KitchenFlavorDto.ImageUrl).
/// - Propagação da ImageUrl para o cardápio público (PublicMenuService / OrderService: PublicPizzaDto e PublicBeverageDto).
/// - Isolamento multi-tenant de produtos com imagem.
/// </summary>
public sealed class ProductImageTests
{
    [Fact]
    public async Task Product_CreateAndUpdate_PersistsTrimmedImageUrl_AndHandlesNullOrWhitespace()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var owner = await TestClient.RegisterAsync(client);
        var token = owner.AccessToken;

        // 1. Criação com URL válida e espaços nas pontas -> deve persistir com Trim()
        var rawUrl = "   https://images.unsplash.com/photo-pizza-calabresa.jpg   ";
        var expectedTrimmed = "https://images.unsplash.com/photo-pizza-calabresa.jpg";

        var createRes = await client.WithToken(token).PostAsJsonAsync("/api/v1/products", new
        {
            name = "Calabresa Especial",
            category = "Pizza",
            isActive = true,
            imageUrl = rawUrl
        });
        createRes.EnsureSuccessStatusCode();

        using (var createJson = await TestClient.ReadJsonAsync(createRes))
        {
            var created = createJson.RootElement;
            Assert.Equal("Calabresa Especial", created.GetProperty("name").GetString());
            Assert.Equal(expectedTrimmed, created.GetProperty("imageUrl").GetString());
        }

        // 2. Criação com imageUrl nula -> persiste nulo
        var createNullRes = await client.WithToken(token).PostAsJsonAsync("/api/v1/products", new
        {
            name = "Muçarela Tradicional",
            category = "Pizza",
            isActive = true,
            imageUrl = (string?)null
        });
        createNullRes.EnsureSuccessStatusCode();

        Guid nullProductId;
        using (var nullJson = await TestClient.ReadJsonAsync(createNullRes))
        {
            nullProductId = nullJson.RootElement.GetProperty("id").GetGuid();
            Assert.Equal(System.Text.Json.JsonValueKind.Null, nullJson.RootElement.GetProperty("imageUrl").ValueKind);
        }

        // 3. Criação com imageUrl em branco ("   ") -> persiste nulo
        var createWhitespaceRes = await client.WithToken(token).PostAsJsonAsync("/api/v1/products", new
        {
            name = "Marguerita Fresca",
            category = "Pizza",
            isActive = true,
            imageUrl = "     "
        });
        createWhitespaceRes.EnsureSuccessStatusCode();

        Guid whitespaceProductId;
        using (var wsJson = await TestClient.ReadJsonAsync(createWhitespaceRes))
        {
            whitespaceProductId = wsJson.RootElement.GetProperty("id").GetGuid();
            Assert.Equal(System.Text.Json.JsonValueKind.Null, wsJson.RootElement.GetProperty("imageUrl").ValueKind);
        }

        // 4. Atualização: de nulo para uma nova URL com trim
        var newUrl = "   https://cdn.pizzaria.com/mucarela.webp  ";
        var updateRes = await client.WithToken(token).PutAsJsonAsync($"/api/v1/products/{nullProductId}", new
        {
            name = "Muçarela Tradicional Especial",
            category = "Pizza",
            isActive = true,
            imageUrl = newUrl
        });
        updateRes.EnsureSuccessStatusCode();

        using (var updateJson = await TestClient.ReadJsonAsync(updateRes))
        {
            Assert.Equal("https://cdn.pizzaria.com/mucarela.webp", updateJson.RootElement.GetProperty("imageUrl").GetString());
        }

        // 5. Atualização: de URL para nulo (remover imagem)
        var removeImageRes = await client.WithToken(token).PutAsJsonAsync($"/api/v1/products/{nullProductId}", new
        {
            name = "Muçarela Tradicional Especial",
            category = "Pizza",
            isActive = true,
            imageUrl = (string?)null
        });
        removeImageRes.EnsureSuccessStatusCode();

        using (var removeJson = await TestClient.ReadJsonAsync(removeImageRes))
        {
            Assert.Equal(System.Text.Json.JsonValueKind.Null, removeJson.RootElement.GetProperty("imageUrl").ValueKind);
        }

        // 6. Atualização: de URL para apenas espaços -> vira nulo
        var updateWhitespaceRes = await client.WithToken(token).PutAsJsonAsync($"/api/v1/products/{whitespaceProductId}", new
        {
            name = "Marguerita Fresca",
            category = "Pizza",
            isActive = true,
            imageUrl = "   \t \n  "
        });
        updateWhitespaceRes.EnsureSuccessStatusCode();

        using (var wsUpdatedJson = await TestClient.ReadJsonAsync(updateWhitespaceRes))
        {
            Assert.Equal(System.Text.Json.JsonValueKind.Null, wsUpdatedJson.RootElement.GetProperty("imageUrl").ValueKind);
        }

        // 7. Listagem geral retorna ImageUrl correta de cada produto
        var listRes = await client.WithToken(token).GetAsync("/api/v1/products");
        listRes.EnsureSuccessStatusCode();

        using var listJson = await TestClient.ReadJsonAsync(listRes);
        var items = listJson.RootElement.EnumerateArray().ToList();
        var calabresa = items.FirstOrDefault(i => i.GetProperty("name").GetString() == "Calabresa Especial");
        Assert.Equal(expectedTrimmed, calabresa.GetProperty("imageUrl").GetString());
    }

    [Fact]
    public async Task KitchenQueue_ReturnsImageUrl_ForFlavorsWithImageConfigured()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var owner = await TestClient.RegisterAsync(client);
        var token = owner.AccessToken;

        // Insumos e estoque
        var mucarela = await CreateIngredientAsync(client, token, "Muçarela", "g", 5000, 32.90m);
        var calabresa = await CreateIngredientAsync(client, token, "Calabresa", "g", 1000, 9.50m);
        await AddInitialStockAsync(client, token, mucarela, 5000);
        await AddInitialStockAsync(client, token, calabresa, 1000);

        var size = await CreateSizeAsync(client, token, "Grande");

        // Produto A com imagem, Produto B sem imagem
        var imgCalabresa = "https://images.unsplash.com/photo-calabresa.jpg";
        var saborCalabresa = await CreatePizzaAsync(client, token, "Calabresa Crocante", imgCalabresa);
        var saborMucarela = await CreatePizzaAsync(client, token, "Muçarela Pura", null);

        await CreateSheetAsync(client, token, saborCalabresa, size, (calabresa, 120m, "g"));
        await CreateSheetAsync(client, token, saborMucarela, size, (mucarela, 180m, "g"));

        // Pedido meio a meio
        var (orderId, _) = await CreateOrderAsync(
            client, token, size, (saborCalabresa, 50m), (saborMucarela, 50m));

        // Consulta à fila da cozinha
        var queueRes = await client.WithToken(token).GetAsync("/api/v1/kitchen/queue");
        queueRes.EnsureSuccessStatusCode();

        using var queueJson = await TestClient.ReadJsonAsync(queueRes);
        var root = queueJson.RootElement;
        var queueItem = root.GetProperty("items").EnumerateArray()
            .FirstOrDefault(o => o.GetProperty("id").GetGuid() == orderId);

        Assert.True(queueItem.ValueKind != System.Text.Json.JsonValueKind.Undefined);
        var flavors = queueItem.GetProperty("items")[0].GetProperty("flavors").EnumerateArray().ToList();
        Assert.Equal(2, flavors.Count);

        var flavorCalabresa = flavors.First(f => f.GetProperty("productName").GetString() == "Calabresa Crocante");
        Assert.Equal(imgCalabresa, flavorCalabresa.GetProperty("imageUrl").GetString());

        var flavorMucarela = flavors.First(f => f.GetProperty("productName").GetString() == "Muçarela Pura");
        Assert.Equal(System.Text.Json.JsonValueKind.Null, flavorMucarela.GetProperty("imageUrl").ValueKind);
    }

    [Fact]
    public async Task PublicMenu_ReturnsImageUrl_ForPizzasAndBeverages()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using var anonClient = factory.CreateClient();

        var owner = await TestClient.RegisterAsync(client, "Pizzaria Imagem Show");
        var token = owner.AccessToken;

        // Obter slug da pizzaria
        var tenantRes = await client.WithToken(token).GetAsync("/api/v1/tenant");
        tenantRes.EnsureSuccessStatusCode();
        using var tenantJson = await TestClient.ReadJsonAsync(tenantRes);
        var slug = tenantJson.RootElement.GetProperty("slug").GetString();
        Assert.False(string.IsNullOrWhiteSpace(slug));

        // Criar ingredientes, tamanhos e produtos
        var queijo = await CreateIngredientAsync(client, token, "Queijo Prato", "g", 2000, 25.00m);
        var refriEstoque = await CreateIngredientAsync(client, token, "Lata Guaraná", "un", 50, 2.50m);
        await AddInitialStockAsync(client, token, queijo, 2000);
        await AddInitialStockAsync(client, token, refriEstoque, 50);

        var size = await CreateSizeAsync(client, token, "Média");

        var pizzaImg = "https://images.unsplash.com/photo-pizza-marguerita.webp";
        var pizzaComImg = await CreatePizzaAsync(client, token, "Marguerita Gourmet", pizzaImg);
        var pizzaSemImg = await CreatePizzaAsync(client, token, "Queijo Simples", null);

        await CreateSheetAsync(client, token, pizzaComImg, size, (queijo, 150m, "g"));
        await CreateSheetAsync(client, token, pizzaSemImg, size, (queijo, 120m, "g"));

        var refriImg = "https://images.unsplash.com/photo-guarana-lata.png";
        var refriComImg = await CreateBeverageAsync(client, token, "Guaraná Lata 350ml", 3.00m, 2.0m, refriImg);
        var refriSemImg = await CreateBeverageAsync(client, token, "Água Mineral 500ml", 1.50m, 2.0m, null);

        // Acessar cardápio público anonimamente
        var menuRes = await anonClient.GetAsync($"/api/v1/public/{slug}/menu");
        menuRes.EnsureSuccessStatusCode();

        using var menuJson = await TestClient.ReadJsonAsync(menuRes);
        var root = menuJson.RootElement;

        // 1. Verificar pizzas no cardápio público
        var pizzas = root.GetProperty("pizzas").EnumerateArray().ToList();
        var pizzaDtoCom = pizzas.First(p => p.GetProperty("productId").GetGuid() == pizzaComImg);
        Assert.Equal(pizzaImg, pizzaDtoCom.GetProperty("imageUrl").GetString());

        var pizzaDtoSem = pizzas.First(p => p.GetProperty("productId").GetGuid() == pizzaSemImg);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, pizzaDtoSem.GetProperty("imageUrl").ValueKind);

        // 2. Verificar bebidas no cardápio público
        var beverages = root.GetProperty("beverages").EnumerateArray().ToList();
        var bevDtoCom = beverages.First(b => b.GetProperty("productId").GetGuid() == refriComImg);
        Assert.Equal(refriImg, bevDtoCom.GetProperty("imageUrl").GetString());

        var bevDtoSem = beverages.First(b => b.GetProperty("productId").GetGuid() == refriSemImg);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, bevDtoSem.GetProperty("imageUrl").ValueKind);
    }

    [Fact]
    public async Task CrossTenant_ProductWithImageUrl_IsIsolatedBetweenTenants()
    {
        using var factory = new ApiFactory();
        using var clientA = factory.CreateClient();
        using var clientB = factory.CreateClient();

        var tenantA = await TestClient.RegisterAsync(clientA, "Pizzaria Alfa");
        var tenantB = await TestClient.RegisterAsync(clientB, "Pizzaria Beta");

        // Tenant A cria produto com imagem
        var imgAlfa = "https://cdn.alfa.com/pizza-alfa.jpg";
        var productAId = await CreatePizzaAsync(clientA, tenantA.AccessToken, "Pizza Exclusiva Alfa", imgAlfa);

        // Tenant B não enxerga o produto do Tenant A na sua listagem
        var listBRes = await clientB.WithToken(tenantB.AccessToken).GetAsync("/api/v1/products");
        listBRes.EnsureSuccessStatusCode();

        using var listBJson = await TestClient.ReadJsonAsync(listBRes);
        Assert.DoesNotContain(listBJson.RootElement.EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == productAId);

        // Tenant B não consegue atualizar a imagem ou dados do produto de Tenant A (404)
        var updateBRes = await clientB.WithToken(tenantB.AccessToken).PutAsJsonAsync($"/api/v1/products/{productAId}", new
        {
            name = "Hackeado",
            category = "Pizza",
            isActive = true,
            imageUrl = "https://hack.com/malicious.jpg"
        });
        await TestClient.AssertErrorEnvelopeAsync(updateBRes, 404);
    }
}

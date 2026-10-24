using System.Net;
using System.Net.Http.Json;

using static PizzariaLucrativa.Api.Tests.OrderFactory;

namespace PizzariaLucrativa.Api.Tests;

/// <summary>
/// Docs/10, Sprints 4 a 7:
/// - Sprint 4: idempotência via <c>Idempotency-Key</c>, resumo <c>/orders/today</c>,
///   comanda <c>/orders/{id}/ticket</c>, fluxo do atendente e validação de desconto.
/// - Sprint 5: cardápio público, checkout anônimo ignorando preços adulterados,
///   acompanhamento por código, isolamento de código entre tenants e rate limit 429.
/// - Sprint 6: relatório de lucro real (<c>owner</c> 200, <c>atendente</c>/<c>pizzaiolo</c> 403),
///   exclusão de cancelados, período sem pedidos (sem divisão por zero) e séries por dia/produto/canal.
/// - Sprint 7: concorrência de dois pizzaiolos clicando "Pronto" simultaneamente (baixa única).
/// </summary>
public sealed class Sprints4To7IntegrationTests
{
    private static async Task<string> CreateAndLoginUserAsync(
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
        return json.RootElement.GetProperty("accessToken").GetString()!;
    }

    private static async Task<string> GetSlugAsync(HttpClient client, string token)
    {
        var res = await client.WithToken(token).GetAsync("/api/v1/tenant");
        res.EnsureSuccessStatusCode();
        using var json = await TestClient.ReadJsonAsync(res);
        return json.RootElement.GetProperty("slug").GetString()!;
    }

    [Fact]
    public async Task Sprint4_IdempotencyKey_Ticket_TodaySummary_AndDiscountValidation()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var owner = await TestClient.RegisterAsync(client, "Pizzaria Balcão");
        var ownerToken = owner.AccessToken;
        var atendenteToken = await CreateAndLoginUserAsync(client, ownerToken, "atendente", "Ana Atendente");

        // Configura taxa de entrega de R$ 8,00
        var feeRes = await client.WithToken(ownerToken).PutAsJsonAsync("/api/v1/fees", new
        {
            cardFeePercentage = 2.99m,
            deliveryFeePercentage = 0m,
            deliveryFixedValue = 8.00m,
            defaultMarginPercentage = 60m,
            lossPercentageGlobal = 0m
        });
        feeRes.EnsureSuccessStatusCode();

        var mucarela = await CreateIngredientAsync(client, ownerToken, "Muçarela", "g", 5000, 32.90m);
        var size = await CreateSizeAsync(client, ownerToken, "Grande");
        var pizza = await CreatePizzaAsync(client, ownerToken, "Muçarela");
        await CreateSheetAsync(client, ownerToken, pizza, size, (mucarela, 200m, "g"));

        // 1. Atendente envia POST /orders duas vezes com a mesma Idempotency-Key → não duplica.
        var idempKey = $"idem-{Guid.NewGuid():N}";
        var orderPayload = new
        {
            type = "Delivery",
            customerName = "Cliente Teste",
            customerPhone = "11999990000",
            addressDistrict = "Centro",
            addressStreet = "Rua A",
            addressNumber = "10",
            addressComplement = "Casa 2",
            notes = "Tocar interfone",
            paymentMethod = "Pix",
            discount = 2.00m,
            items = new[]
            {
                new
                {
                    sizeId = (Guid?)size,
                    comboId = (Guid?)null,
                    quantity = 1,
                    notes = "Bem assada",
                    flavors = new[] { new { productId = pizza, sharePercentage = 100m } }
                }
            }
        };

        using var req1 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders")
        {
            Content = JsonContent.Create(orderPayload)
        };
        req1.Headers.Add("Idempotency-Key", idempKey);

        using var req2 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders")
        {
            Content = JsonContent.Create(orderPayload)
        };
        req2.Headers.Add("Idempotency-Key", idempKey);

        var res1 = await client.WithToken(atendenteToken).SendAsync(req1);
        var res2 = await client.WithToken(atendenteToken).SendAsync(req2);
        res1.EnsureSuccessStatusCode();
        res2.EnsureSuccessStatusCode();

        using var json1 = await TestClient.ReadJsonAsync(res1);
        using var json2 = await TestClient.ReadJsonAsync(res2);
        var orderId = json1.RootElement.GetProperty("id").GetGuid();
        var code = json1.RootElement.GetProperty("code").GetString()!;

        Assert.Equal(orderId, json2.RootElement.GetProperty("id").GetGuid());
        Assert.Equal(code, json2.RootElement.GetProperty("code").GetString());

        // 2. Resumo do dia (/orders/today) reflete exatamente 1 pedido (sem duplicação).
        var todayRes = await client.WithToken(atendenteToken).GetAsync("/api/v1/orders/today");
        todayRes.EnsureSuccessStatusCode();
        using (var todayJson = await TestClient.ReadJsonAsync(todayRes))
        {
            Assert.Equal(1, todayJson.RootElement.GetProperty("totalOrders").GetInt32());
            Assert.Equal(1, todayJson.RootElement.GetProperty("received").GetInt32());
            Assert.Equal(1, todayJson.RootElement.GetProperty("delivery").GetInt32());
        }

        // 3. Comanda imprimível (/orders/{id}/ticket) traz aviso fiscal e não vaza custo/margem.
        var ticketRes = await client.WithToken(atendenteToken).GetAsync($"/api/v1/orders/{orderId}/ticket");
        ticketRes.EnsureSuccessStatusCode();
        var rawTicket = await ticketRes.Content.ReadAsStringAsync();
        Assert.DoesNotContain("cost", rawTicket, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("margin", rawTicket, StringComparison.OrdinalIgnoreCase);

        using (var ticketJson = await TestClient.ReadJsonAsync(ticketRes))
        {
            Assert.Equal("Pizzaria Balcão", ticketJson.RootElement.GetProperty("pizzeriaName").GetString());
            Assert.Equal(code, ticketJson.RootElement.GetProperty("code").GetString());
            Assert.Equal("Não é documento fiscal", ticketJson.RootElement.GetProperty("disclaimer").GetString());
            Assert.Equal(8.00m, ticketJson.RootElement.GetProperty("deliveryFee").GetDecimal());
            Assert.Equal(2.00m, ticketJson.RootElement.GetProperty("discount").GetDecimal());
        }

        // 4. Desconto ≥ subtotal é rejeitado com 400.
        var badDiscountRes = await client.WithToken(atendenteToken).PostAsJsonAsync("/api/v1/orders", new
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
            discount = 9999m,
            items = new[]
            {
                new
                {
                    sizeId = (Guid?)size,
                    comboId = (Guid?)null,
                    quantity = 1,
                    notes = (string?)null,
                    flavors = new[] { new { productId = pizza, sharePercentage = 100m } }
                }
            }
        });
        await TestClient.AssertErrorEnvelopeAsync(badDiscountRes, 400);
    }

    [Fact]
    public async Task Sprint5_PublicCheckout_IgnoresAdulteratedPrice_EnforcesTenantIsolation_AndRateLimits()
    {
        using var factory = new ApiFactory();
        using var ownerClient = factory.CreateClient();
        using var anonClient = factory.CreateClient();

        var tenantA = await TestClient.RegisterAsync(ownerClient, "Pizzaria Publica A");
        var tokenA = tenantA.AccessToken;
        var slugA = await GetSlugAsync(ownerClient, tokenA);

        var tenantB = await TestClient.RegisterAsync(ownerClient, "Pizzaria Publica B");
        var tokenB = tenantB.AccessToken;
        var slugB = await GetSlugAsync(ownerClient, tokenB);

        var mucarela = await CreateIngredientAsync(ownerClient, tokenA, "Muçarela", "g", 5000, 32.90m);
        var size = await CreateSizeAsync(ownerClient, tokenA, "Grande");
        var pizza = await CreatePizzaAsync(ownerClient, tokenA, "Margherita");
        await CreateSheetAsync(ownerClient, tokenA, pizza, size, (mucarela, 200m, "g"));

        // 1. Cardápio público expõe preço de venda calculado e nunca expõe custo/margem.
        var menuRes = await anonClient.GetAsync($"/api/v1/public/{slugA}/menu");
        menuRes.EnsureSuccessStatusCode();
        var rawMenu = await menuRes.Content.ReadAsStringAsync();
        Assert.DoesNotContain("cost", rawMenu, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("margin", rawMenu, StringComparison.OrdinalIgnoreCase);

        decimal serverPrice;
        using (var menuJson = await TestClient.ReadJsonAsync(menuRes))
        {
            serverPrice = menuJson.RootElement
                .GetProperty("pizzas")[0]
                .GetProperty("pricesBySize")[0]
                .GetProperty("price").GetDecimal();
            Assert.True(serverPrice > 0m);
        }

        // 2. Cliente mal-intencionado tenta enviar unitPrice=0.01, total=0.01 e discount=50.00 no body:
        //    o servidor ignora e cobra exatamente o preço da ficha técnica!
        var publicOrderRes = await anonClient.PostAsJsonAsync($"/api/v1/public/{slugA}/orders", new
        {
            type = "Pickup",
            customerName = "Maria Anônima",
            customerPhone = "11988887777",
            addressDistrict = (string?)null,
            addressStreet = (string?)null,
            addressNumber = (string?)null,
            addressComplement = (string?)null,
            notes = "Retirada rápida",
            paymentMethod = "Pix",
            discount = 50.00m,
            total = 0.01m,
            items = new[]
            {
                new
                {
                    sizeId = (Guid?)size,
                    comboId = (Guid?)null,
                    quantity = 2,
                    notes = (string?)null,
                    unitPrice = 0.01m,
                    total = 0.01m,
                    flavors = new[] { new { productId = pizza, sharePercentage = 100m } }
                }
            }
        });
        publicOrderRes.EnsureSuccessStatusCode();

        string codeA;
        using (var orderJson = await TestClient.ReadJsonAsync(publicOrderRes))
        {
            codeA = orderJson.RootElement.GetProperty("code").GetString()!;
            Assert.Equal(serverPrice * 2, orderJson.RootElement.GetProperty("total").GetDecimal());
            Assert.Equal(0m, orderJson.RootElement.GetProperty("discount").GetDecimal());
        }

        // 3. Acompanhamento público por código funciona em A e não expõe telefone/endereço.
        var trackRes = await anonClient.GetAsync($"/api/v1/public/{slugA}/orders/{codeA}");
        trackRes.EnsureSuccessStatusCode();
        var rawTrack = await trackRes.Content.ReadAsStringAsync();
        Assert.DoesNotContain("11988887777", rawTrack);
        Assert.DoesNotContain("customerPhone", rawTrack, StringComparison.OrdinalIgnoreCase);

        // 4. Isolamento entre tenants: código de A consultado no slug de B → 404; código inexistente → 404.
        var crossTenantRes = await anonClient.GetAsync($"/api/v1/public/{slugB}/orders/{codeA}");
        await TestClient.AssertErrorEnvelopeAsync(crossTenantRes, 404);

        var wrongCodeRes = await anonClient.GetAsync($"/api/v1/public/{slugA}/orders/ZZZZZZ");
        await TestClient.AssertErrorEnvelopeAsync(wrongCodeRes, 404);

        // 5. Rate limit: após 10 POSTs no minuto em /public/{slug}/orders, a 11ª chamada recebe 429.
        HttpResponseMessage? lastResponse = null;
        for (var i = 0; i < 11; i++)
        {
            lastResponse = await anonClient.PostAsJsonAsync($"/api/v1/public/{slugB}/orders", new
            {
                type = "Pickup",
                customerName = "Teste Rate Limit",
                customerPhone = "11977776666",
                items = Array.Empty<object>()
            });
            if (lastResponse.StatusCode == HttpStatusCode.TooManyRequests)
                break;
        }

        Assert.NotNull(lastResponse);
        await TestClient.AssertErrorEnvelopeAsync(lastResponse!, 429);
    }

    [Fact]
    public async Task Sprint5_PublicCheckout_Delivery_WithFreeformAddress_OrStructuredAddress_Succeeds()
    {
        using var factory = new ApiFactory();
        using var ownerClient = factory.CreateClient();
        using var anonClient = factory.CreateClient();

        var ownerA = await TestClient.RegisterAsync(ownerClient, "Pizzaria Delivery Test");
        var tokenA = ownerA.AccessToken;
        var slugA = await GetSlugAsync(ownerClient, tokenA);

        var mucarela = await CreateIngredientAsync(ownerClient, tokenA, "Muçarela Delivery", "g", 5000, 32.90m);
        var size = await CreateSizeAsync(ownerClient, tokenA, "Grande");
        var pizza = await CreatePizzaAsync(ownerClient, tokenA, "Muçarela Delivery");
        await CreateSheetAsync(ownerClient, tokenA, pizza, size, (mucarela, 200m, "g"));

        // 1. Envio com string livre em deliveryAddress (ex.: "rua iris 29b, jd vitoria , b") sem campos estruturados
        var freeformRes = await anonClient.PostAsJsonAsync($"/api/v1/public/{slugA}/orders", new
        {
            type = "Delivery",
            customerName = "Cliente Endereço Livre",
            customerPhone = "44999246859",
            deliveryAddress = "rua iris 29b, jd vitoria , b",
            paymentMethod = "Pix",
            items = new[]
            {
                new
                {
                    sizeId = (Guid?)size,
                    comboId = (Guid?)null,
                    quantity = 1,
                    flavors = new[] { new { productId = pizza, sharePercentage = 100m } }
                }
            }
        });
        freeformRes.EnsureSuccessStatusCode();

        using (var freeformJson = await TestClient.ReadJsonAsync(freeformRes))
        {
            var street = freeformJson.RootElement.GetProperty("addressStreet").GetString();
            var district = freeformJson.RootElement.GetProperty("addressDistrict").GetString();
            var number = freeformJson.RootElement.GetProperty("addressNumber").GetString();
            Assert.Equal("rua iris", street);
            Assert.Equal("29b", number);
            Assert.Equal("jd vitoria", district);
        }

        // 2. Envio com campos estruturados (Rua, Número, Bairro, Complemento)
        var structuredRes = await anonClient.PostAsJsonAsync($"/api/v1/public/{slugA}/orders", new
        {
            type = "Delivery",
            customerName = "Cliente Estruturado",
            customerPhone = "11988887777",
            addressStreet = "Rua das Flores",
            addressNumber = "123",
            addressDistrict = "Jardim das Rosas",
            addressComplement = "Apto 42",
            deliveryAddress = "Rua das Flores, 123 - Jardim das Rosas (Apto 42)",
            paymentMethod = "Card",
            items = new[]
            {
                new
                {
                    sizeId = (Guid?)size,
                    comboId = (Guid?)null,
                    quantity = 1,
                    flavors = new[] { new { productId = pizza, sharePercentage = 100m } }
                }
            }
        });
        structuredRes.EnsureSuccessStatusCode();

        using (var structuredJson = await TestClient.ReadJsonAsync(structuredRes))
        {
            Assert.Equal("Rua das Flores", structuredJson.RootElement.GetProperty("addressStreet").GetString());
            Assert.Equal("123", structuredJson.RootElement.GetProperty("addressNumber").GetString());
            Assert.Equal("Jardim das Rosas", structuredJson.RootElement.GetProperty("addressDistrict").GetString());
            Assert.Equal("Apto 42", structuredJson.RootElement.GetProperty("addressComplement").GetString());
        }
    }

    [Fact]
    public async Task Sprint6And7_ProfitReport_RoleAccess_ExcludesCancelled_AndConcurrentReadyConsumesOnce()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var owner = await TestClient.RegisterAsync(client, "Pizzaria Lucro Real");
        var ownerToken = owner.AccessToken;
        var atendenteToken = await CreateAndLoginUserAsync(client, ownerToken, "atendente", "Ana");
        var pizzaioloToken = await CreateAndLoginUserAsync(client, ownerToken, "pizzaiolo", "Pedro");

        // 1. Atendente e pizzaiolo recebem 403 no relatório de lucro.
        var forbiddenAtendente = await client.WithToken(atendenteToken).GetAsync("/api/v1/reports/profit");
        await TestClient.AssertErrorEnvelopeAsync(forbiddenAtendente, 403);

        var forbiddenPizzaiolo = await client.WithToken(pizzaioloToken).GetAsync("/api/v1/reports/profit");
        await TestClient.AssertErrorEnvelopeAsync(forbiddenPizzaiolo, 403);

        // 2. Período vazio não causa divisão por zero.
        var emptyReport = await client.WithToken(ownerToken)
            .GetAsync("/api/v1/reports/profit?from=2020-01-01&to=2020-01-05");
        emptyReport.EnsureSuccessStatusCode();
        using (var emptyJson = await TestClient.ReadJsonAsync(emptyReport))
        {
            Assert.Equal(0, emptyJson.RootElement.GetProperty("completedOrdersCount").GetInt32());
            Assert.Equal(0m, emptyJson.RootElement.GetProperty("revenue").GetDecimal());
            Assert.Equal(0m, emptyJson.RootElement.GetProperty("grossMarginPercentage").GetDecimal());
            Assert.Equal(0m, emptyJson.RootElement.GetProperty("averageTicket").GetDecimal());
        }

        // 3. Cria catálogo e 2 pedidos:
        //    - Pedido 1: concluído (testando também concorrência de 2 cliques simultâneos em "Pronto" — Sprint 7!)
        //    - Pedido 2: cancelado após "Pronto" (estoque estornado, fora do faturamento/custo)
        var mucarela = await CreateIngredientAsync(client, ownerToken, "Muçarela", "g", 5000, 32.90m);
        await AddInitialStockAsync(client, ownerToken, mucarela, 5000);

        var size = await CreateSizeAsync(client, ownerToken, "Grande");
        var pizza = await CreatePizzaAsync(client, ownerToken, "Muçarela");
        await CreateSheetAsync(client, ownerToken, pizza, size, (mucarela, 200m, "g"));

        var (order1Id, _) = await CreateOrderAsync(client, ownerToken, size, (pizza, 100m));
        var (order2Id, _) = await CreateOrderAsync(client, ownerToken, size, (pizza, 100m));

        await client.WithToken(pizzaioloToken).PostAsync($"/api/v1/kitchen/queue/{order1Id}/start", null);

        // Sprint 7: dois pizzaiolos clicam "Pronto" simultaneamente no mesmo pedido → apenas UMA baixa!
        using var concurrentClientA = factory.CreateClient().WithToken(pizzaioloToken);
        using var concurrentClientB = factory.CreateClient().WithToken(pizzaioloToken);

        var readyResponses = await Task.WhenAll(
            concurrentClientA.PostAsync($"/api/v1/kitchen/queue/{order1Id}/ready", null),
            concurrentClientB.PostAsync($"/api/v1/kitchen/queue/{order1Id}/ready", null));

        foreach (var r in readyResponses)
            r.EnsureSuccessStatusCode();

        // Estoque caiu apenas 200g (5000 -> 4800), nunca 400g.
        Assert.Equal(4800m, await GetBalanceAsync(client, ownerToken, mucarela));

        // Conclui o Pedido 1.
        var finish1 = await client.WithToken(pizzaioloToken)
            .PostAsync($"/api/v1/kitchen/queue/{order1Id}/finish", null);
        finish1.EnsureSuccessStatusCode();

        // Pedido 2 passa por Pronto e depois é cancelado (estorna os 200g).
        await client.WithToken(pizzaioloToken).PostAsync($"/api/v1/kitchen/queue/{order2Id}/start", null);
        await client.WithToken(pizzaioloToken).PostAsync($"/api/v1/kitchen/queue/{order2Id}/ready", null);
        var cancel2 = await client.WithToken(ownerToken)
            .PostAsJsonAsync($"/api/v1/orders/{order2Id}/cancel", new { reason = "Erro de digitação" });
        cancel2.EnsureSuccessStatusCode();

        Assert.Equal(4800m, await GetBalanceAsync(client, ownerToken, mucarela));

        // 4. Relatório de lucro contabiliza apenas o Pedido 1 (Completed) e ignora o Pedido 2 (Cancelled).
        var reportRes = await client.WithToken(ownerToken).GetAsync("/api/v1/reports/profit");
        reportRes.EnsureSuccessStatusCode();
        using (var reportJson = await TestClient.ReadJsonAsync(reportRes))
        {
            var root = reportJson.RootElement;
            Assert.Equal(1, root.GetProperty("completedOrdersCount").GetInt32());
            Assert.Equal(1, root.GetProperty("cancelledOrdersCount").GetInt32());
            Assert.True(root.GetProperty("revenue").GetDecimal() > 0m);
            Assert.True(root.GetProperty("ingredientCost").GetDecimal() > 0m);
            Assert.True(root.GetProperty("grossProfit").GetDecimal() > 0m);
            Assert.False(root.GetProperty("hasCostDivergence").GetBoolean());
        }

        // Séries complementares (/by-day, /by-product, /by-channel) respondem 200.
        var byDay = await client.WithToken(ownerToken).GetAsync("/api/v1/reports/profit/by-day");
        byDay.EnsureSuccessStatusCode();

        var byProduct = await client.WithToken(ownerToken).GetAsync("/api/v1/reports/profit/by-product");
        byProduct.EnsureSuccessStatusCode();
        using (var prodJson = await TestClient.ReadJsonAsync(byProduct))
        {
            Assert.Equal(1, prodJson.RootElement.GetArrayLength());
            Assert.Equal("Muçarela", prodJson.RootElement[0].GetProperty("productName").GetString());
        }

        var byChannel = await client.WithToken(ownerToken).GetAsync("/api/v1/reports/profit/by-channel");
        byChannel.EnsureSuccessStatusCode();
    }
}

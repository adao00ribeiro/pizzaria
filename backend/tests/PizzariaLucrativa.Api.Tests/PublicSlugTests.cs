using System.Net;
using System.Net.Http.Json;

namespace PizzariaLucrativa.Api.Tests;

/// <summary>
/// Docs/10, Sprint 1: resolução pública de tenant pelo slug em /api/v1/public/{slug}.
/// </summary>
public sealed class PublicSlugTests
{
    [Fact]
    public async Task UnknownSlug_Returns404_WithStandardEnvelope()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/public/nao-existe/menu");
        await TestClient.AssertErrorEnvelopeAsync(response, 404);
    }

    [Fact]
    public async Task ValidSlug_ResolvesTenant_WithoutAuthentication()
    {
        using var factory = new ApiFactory();
        using var ownerClient = factory.CreateClient();
        using var anonClient = factory.CreateClient();

        var owner = await TestClient.RegisterAsync(ownerClient, "Pizzaria do Zé & Cia");

        // Slug gerado no register.
        var tenant = await ownerClient
            .WithToken(owner.AccessToken)
            .GetAsync("/api/v1/tenant");
        tenant.EnsureSuccessStatusCode();

        using var json = await TestClient.ReadJsonAsync(tenant);
        var slug = json.RootElement.GetProperty("slug").GetString();
        Assert.False(string.IsNullOrWhiteSpace(slug));

        // Rota pública sem JWT resolve o tenant pelo slug.
        var anonymous = await anonClient.GetAsync($"/api/v1/public/{slug}/menu");
        anonymous.EnsureSuccessStatusCode();

        using var publicJson = await TestClient.ReadJsonAsync(anonymous);
        Assert.Equal("Pizzaria do Zé & Cia", publicJson.RootElement.GetProperty("name").GetString());

        // Slug com caixa diferente também resolve.
        var upper = await anonClient.GetAsync($"/api/v1/public/{slug!.ToUpperInvariant()}/menu");
        upper.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task PublicMenu_IsScopedToItsOwnSlug()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using var anonClient = factory.CreateClient();

        var tenantA = await TestClient.RegisterAsync(client, "Pizzaria A");
        var tenantB = await TestClient.RegisterAsync(client, "Pizzaria B");

        var slugA = await GetSlugAsync(client, tenantA.AccessToken);
        var slugB = await GetSlugAsync(client, tenantB.AccessToken);

        using var menuA = await TestClient.ReadJsonAsync(
            await anonClient.GetAsync($"/api/v1/public/{slugA}/menu"));
        Assert.Equal("Pizzaria A", menuA.RootElement.GetProperty("name").GetString());

        using var menuB = await TestClient.ReadJsonAsync(
            await anonClient.GetAsync($"/api/v1/public/{slugB}/menu"));
        Assert.Equal("Pizzaria B", menuB.RootElement.GetProperty("name").GetString());

        // Slug inexistente continua 404 mesmo "parecido" com o de A.
        var missing = await anonClient.GetAsync($"/api/v1/public/{slugA}-2/menu");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Slug_IsUniqueAndEditable()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using var anonClient = factory.CreateClient();

        var tenantA = await TestClient.RegisterAsync(client, "Pizzaria A");
        var slugA = await GetSlugAsync(client, tenantA.AccessToken);

        // Outra pizzaria não pode reivindicar o mesmo slug.
        var tenantB = await TestClient.RegisterAsync(client, "Pizzaria B");
        var conflict = await client
            .WithToken(tenantB.AccessToken)
            .PutAsJsonAsync("/api/v1/tenant", new { name = "Pizzaria B", slug = slugA });
        await TestClient.AssertErrorEnvelopeAsync(conflict, 409);

        // Slug inválido (formato) → 400 com errors.
        var invalid = await client
            .WithToken(tenantB.AccessToken)
            .PutAsJsonAsync("/api/v1/tenant", new { name = "Pizzaria B", slug = "-x-" });
        await TestClient.AssertErrorEnvelopeAsync(invalid, 400);

        // Dono edita o próprio slug.
        var newSlug = $"pizzaria-{Guid.NewGuid():N}"[..20];
        var update = await client
            .WithToken(tenantB.AccessToken)
            .PutAsJsonAsync("/api/v1/tenant", new { name = "Pizzaria B", slug = newSlug });
        update.EnsureSuccessStatusCode();

        using var json = await TestClient.ReadJsonAsync(update);
        Assert.Equal(newSlug, json.RootElement.GetProperty("slug").GetString());

        var menu = await anonClient.GetAsync($"/api/v1/public/{newSlug}/menu");
        menu.EnsureSuccessStatusCode();

        using var menuJson = await TestClient.ReadJsonAsync(menu);
        Assert.Equal("Pizzaria B", menuJson.RootElement.GetProperty("name").GetString());
    }

    private static async Task<string> GetSlugAsync(HttpClient client, string token)
    {
        var response = await client.WithToken(token).GetAsync("/api/v1/tenant");
        response.EnsureSuccessStatusCode();

        using var json = await TestClient.ReadJsonAsync(response);
        var slug = json.RootElement.GetProperty("slug").GetString();
        Assert.False(string.IsNullOrWhiteSpace(slug));
        return slug!;
    }
}

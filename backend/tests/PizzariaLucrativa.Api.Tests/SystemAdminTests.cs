using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Interfaces;
using PizzariaLucrativa.Domain.Entities;
using PizzariaLucrativa.Infrastructure.Persistence;

namespace PizzariaLucrativa.Api.Tests;

public sealed class SystemAdminTests
{
    private static async Task<string> LoginSystemAdminAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/v1/system-admin/auth/login", new
        {
            email = "admin@plataforma.com",
            password = "Admin@Pizzaria2026!"
        });
        response.EnsureSuccessStatusCode();
        using var json = await TestClient.ReadJsonAsync(response);
        return json.RootElement.GetProperty("token").GetString()!;
    }

    [Fact]
    public async Task SystemAdmin_SingletonConstraint_RejectsSecondAdmin()
    {
        using var factory = new ApiFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Tenta inserir um segundo administrador com outro Guid
        var secondAdmin = new SystemAdmin
        {
            Id = Guid.NewGuid(),
            Email = "segundo@plataforma.com",
            FullName = "Segundo Admin",
            PasswordHash = "hash123",
            CreatedAt = DateTime.UtcNow
        };

        db.SystemAdmins.Add(secondAdmin);

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task SystemAdmin_TenantOwnerCannotAccessSystemAdminEndpoints()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        // Registra um tenant comum (owner)
        var session = await TestClient.RegisterAsync(client);
        var ownerClient = factory.CreateClient().WithToken(session.AccessToken);

        // Tenta acessar endpoints do SystemAdmin
        var responseList = await ownerClient.GetAsync("/api/v1/system-admin/tenants");
        Assert.Equal(HttpStatusCode.Forbidden, responseList.StatusCode);

        var responsePatch = await ownerClient.PatchAsJsonAsync($"/api/v1/system-admin/tenants/{Guid.NewGuid()}/status", new
        {
            isActive = false,
            reason = "Tentativa indevida"
        });
        Assert.Equal(HttpStatusCode.Forbidden, responsePatch.StatusCode);
    }

    [Fact]
    public async Task TenantDeactivation_ImmediateLoginRejection_Returns403()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var email = $"pizzaria-{Guid.NewGuid():N}@teste.com";
        const string password = "Senha123";
        var reg = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            password,
            fullName = "Dono Suspenso",
            pizzeriaName = "Pizzaria A Ser Suspensa"
        });
        reg.EnsureSuccessStatusCode();

        // Autentica como SystemAdmin
        var adminToken = await LoginSystemAdminAsync(client);
        var adminClient = factory.CreateClient().WithToken(adminToken);

        // Obtém o tenant
        var tenantsResponse = await adminClient.GetAsync("/api/v1/system-admin/tenants?search=Pizzaria A Ser Suspensa");
        tenantsResponse.EnsureSuccessStatusCode();
        var paged = await TestClient.ReadAsAsync<PagedResult<TenantListItemDto>>(tenantsResponse);
        var tenant = Assert.Single(paged.Items);

        // Desativa a pizzaria
        var deactResponse = await adminClient.PatchAsJsonAsync($"/api/v1/system-admin/tenants/{tenant.Id}/status", new
        {
            isActive = false,
            reason = "Inadimplência recorrente de mensalidade"
        });
        deactResponse.EnsureSuccessStatusCode();

        // Tenta login como dono da pizzaria desativada
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password
        });

        Assert.Equal(HttpStatusCode.Forbidden, loginResponse.StatusCode);
        await TestClient.AssertErrorEnvelopeAsync(loginResponse, 403);
    }

    [Fact]
    public async Task TenantDeactivation_RefreshRejection_Returns403()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var session = await TestClient.RegisterAsync(client, "Pizzaria Refresh Block");

        var adminToken = await LoginSystemAdminAsync(client);
        var adminClient = factory.CreateClient().WithToken(adminToken);

        var tenantsResponse = await adminClient.GetAsync("/api/v1/system-admin/tenants?search=Pizzaria Refresh Block");
        tenantsResponse.EnsureSuccessStatusCode();
        var paged = await TestClient.ReadAsAsync<PagedResult<TenantListItemDto>>(tenantsResponse);
        var tenant = Assert.Single(paged.Items);

        // Desativa
        var patch = await adminClient.PatchAsJsonAsync($"/api/v1/system-admin/tenants/{tenant.Id}/status", new
        {
            isActive = false,
            reason = "Bloqueio administrativo"
        });
        patch.EnsureSuccessStatusCode();

        // Tenta refresh token
        var refreshResponse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            refreshToken = session.RefreshToken
        });

        Assert.Equal(HttpStatusCode.Forbidden, refreshResponse.StatusCode);
        await TestClient.AssertErrorEnvelopeAsync(refreshResponse, 403);
    }

    [Fact]
    public async Task TenantDeactivation_MiddlewareInterception_Returns403()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var session = await TestClient.RegisterAsync(client, "Pizzaria Middleware Block");
        var authedClient = factory.CreateClient().WithToken(session.AccessToken);

        // Verifica que requisições normais funcionam antes do bloqueio
        var initialCheck = await authedClient.GetAsync("/api/v1/ingredients");
        initialCheck.EnsureSuccessStatusCode();

        var adminToken = await LoginSystemAdminAsync(client);
        var adminClient = factory.CreateClient().WithToken(adminToken);

        var tenantsResponse = await adminClient.GetAsync("/api/v1/system-admin/tenants?search=Pizzaria Middleware Block");
        tenantsResponse.EnsureSuccessStatusCode();
        var paged = await TestClient.ReadAsAsync<PagedResult<TenantListItemDto>>(tenantsResponse);
        var tenant = Assert.Single(paged.Items);

        // Desativa o tenant
        var deact = await adminClient.PatchAsJsonAsync($"/api/v1/system-admin/tenants/{tenant.Id}/status", new
        {
            isActive = false,
            reason = "Violação de termos de uso"
        });
        deact.EnsureSuccessStatusCode();

        // Requisições subsequentes com o JWT ativo devem falhar no middleware com 403
        var intercepted = await authedClient.GetAsync("/api/v1/ingredients");
        Assert.Equal(HttpStatusCode.Forbidden, intercepted.StatusCode);
        await TestClient.AssertErrorEnvelopeAsync(intercepted, 403);
    }

    [Fact]
    public async Task TenantDeactivation_PublicMenuAccess_Returns404()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var session = await TestClient.RegisterAsync(client, "Pizzaria Menu 404");

        var adminToken = await LoginSystemAdminAsync(client);
        var adminClient = factory.CreateClient().WithToken(adminToken);

        var tenantsResponse = await adminClient.GetAsync("/api/v1/system-admin/tenants?search=Pizzaria Menu 404");
        tenantsResponse.EnsureSuccessStatusCode();
        var paged = await TestClient.ReadAsAsync<PagedResult<TenantListItemDto>>(tenantsResponse);
        var tenant = Assert.Single(paged.Items);
        Assert.NotNull(tenant.Slug);

        // Antes da desativação: o cardápio público responde 200 OK
        var activeMenu = await client.GetAsync($"/api/v1/public/{tenant.Slug}/menu");
        Assert.Equal(HttpStatusCode.OK, activeMenu.StatusCode);

        // Desativa
        var deact = await adminClient.PatchAsJsonAsync($"/api/v1/system-admin/tenants/{tenant.Id}/status", new
        {
            isActive = false,
            reason = "Encerramento de atividades"
        });
        deact.EnsureSuccessStatusCode();

        // Cardápio público deve responder 404 Not Found imediatamente
        var inactiveMenu = await client.GetAsync($"/api/v1/public/{tenant.Slug}/menu");
        Assert.Equal(HttpStatusCode.NotFound, inactiveMenu.StatusCode);
    }

    [Fact]
    public async Task TenantListing_ReturnsCorrectOwnerAndMetadata()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var email = $"dono-{Guid.NewGuid():N}@metadados.com";
        const string pizzeriaName = "Pizzaria Metadados";
        var reg = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            password = "Senha123",
            fullName = "Nome Dono Metadados",
            pizzeriaName
        });
        reg.EnsureSuccessStatusCode();

        var adminToken = await LoginSystemAdminAsync(client);
        var adminClient = factory.CreateClient().WithToken(adminToken);

        var response = await adminClient.GetAsync($"/api/v1/system-admin/tenants?search={pizzeriaName}");
        response.EnsureSuccessStatusCode();

        var paged = await TestClient.ReadAsAsync<PagedResult<TenantListItemDto>>(response);
        var item = Assert.Single(paged.Items);

        Assert.Equal(pizzeriaName, item.Name);
        Assert.Equal("Nome Dono Metadados", item.OwnerName);
        Assert.Equal(email, item.OwnerEmail);
        Assert.True(item.IsActive);
        Assert.Null(item.DeactivatedAt);
        Assert.Null(item.DeactivationReason);
    }

    [Fact]
    public async Task TenantReactivation_RestoresAccessAndClearsAudit()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var email = $"reativacao-{Guid.NewGuid():N}@teste.com";
        const string password = "Senha123";
        var reg = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            password,
            fullName = "Dono Reativacao",
            pizzeriaName = "Pizzaria Reativacao"
        });
        reg.EnsureSuccessStatusCode();

        var adminToken = await LoginSystemAdminAsync(client);
        var adminClient = factory.CreateClient().WithToken(adminToken);

        var tenantsResponse = await adminClient.GetAsync("/api/v1/system-admin/tenants?search=Pizzaria Reativacao");
        tenantsResponse.EnsureSuccessStatusCode();
        var paged = await TestClient.ReadAsAsync<PagedResult<TenantListItemDto>>(tenantsResponse);
        var tenant = Assert.Single(paged.Items);

        // Desativa
        var deact = await adminClient.PatchAsJsonAsync($"/api/v1/system-admin/tenants/{tenant.Id}/status", new
        {
            isActive = false,
            reason = "Suspensão temporária"
        });
        deact.EnsureSuccessStatusCode();

        // Login falha
        var failedLogin = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.Forbidden, failedLogin.StatusCode);

        // Reativa
        var react = await adminClient.PatchAsJsonAsync($"/api/v1/system-admin/tenants/{tenant.Id}/status", new
        {
            isActive = true
        });
        react.EnsureSuccessStatusCode();
        var reactivatedItem = await TestClient.ReadAsAsync<TenantListItemDto>(react);

        Assert.True(reactivatedItem.IsActive);
        Assert.Null(reactivatedItem.DeactivatedAt);
        Assert.Null(reactivatedItem.DeactivationReason);

        // Agora login deve funcionar
        var successLogin = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, successLogin.StatusCode);

        // E o cardápio público volta a responder 200
        var publicMenu = await client.GetAsync($"/api/v1/public/{tenant.Slug}/menu");
        Assert.Equal(HttpStatusCode.OK, publicMenu.StatusCode);
    }
}

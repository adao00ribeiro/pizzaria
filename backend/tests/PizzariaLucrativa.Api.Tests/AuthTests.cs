using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace PizzariaLucrativa.Api.Tests;

/// <summary>NF-T01/NF-T02: autenticação, refresh rotativo, 401 e formato de erro.</summary>
public sealed class AuthTests
{
    [Fact]
    public async Task Register_ReturnsTokens_WithFifteenMinuteAccessToken()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var session = await TestClient.RegisterAsync(client);

        Assert.False(string.IsNullOrWhiteSpace(session.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(session.RefreshToken));
        Assert.Equal(15 * 60, session.ExpiresIn);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsSession()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var email = $"dono-{Guid.NewGuid():N}@teste.com";
        const string password = "Senha123";

        var register = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            password,
            fullName = "Dono da Pizzaria",
            pizzeriaName = "Pizzaria Login"
        });
        register.EnsureSuccessStatusCode();

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var json = await TestClient.ReadJsonAsync(login);
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("accessToken").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("refreshToken").GetString()));
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_Returns401Envelope()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = $"ninguem-{Guid.NewGuid():N}@teste.com",
            password = "SenhaErrada123"
        });

        await TestClient.AssertErrorEnvelopeAsync(response, StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task Refresh_RotatesToken_AndRejectsReplay()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var session = await TestClient.RegisterAsync(client);

        var firstRefresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            refreshToken = session.RefreshToken
        });
        firstRefresh.EnsureSuccessStatusCode();

        using var firstJson = await TestClient.ReadJsonAsync(firstRefresh);
        var rotated = firstJson.RootElement.GetProperty("refreshToken").GetString()!;
        var newAccess = firstJson.RootElement.GetProperty("accessToken").GetString()!;
        Assert.NotEqual(session.RefreshToken, rotated);

        // Replay do refresh antigo deve ser rejeitado (rotação de uso único).
        var replay = await client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            refreshToken = session.RefreshToken
        });
        await TestClient.AssertErrorEnvelopeAsync(replay, StatusCodes.Status401Unauthorized);

        // O token novo continua válido em um endpoint protegido.
        var protectedCall = await client
            .WithToken(newAccess)
            .GetAsync("/api/v1/ingredients");
        Assert.Equal(HttpStatusCode.OK, protectedCall.StatusCode);
    }

    [Fact]
    public async Task InvalidBody_Returns400_WithFieldErrors()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var session = await TestClient.RegisterAsync(client);

        var response = await client
            .WithToken(session.AccessToken)
            .PostAsJsonAsync("/api/v1/ingredients", new
            {
                name = "",
                purchaseUnit = "g",
                purchaseQuantity = 0m,
                purchasePrice = 10m,
                lossPercentage = 0m,
                minimumStock = 0m
            });

        await TestClient.AssertErrorEnvelopeAsync(response, StatusCodes.Status400BadRequest);

        using var json = await TestClient.ReadJsonAsync(response);
        var fields = json.RootElement.GetProperty("errors")
            .EnumerateArray()
            .Select(e => e.GetProperty("field").GetString())
            .ToList();

        Assert.Contains("Name", fields);
        Assert.Contains("PurchaseQuantity", fields);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401Envelope()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/ingredients");

        await TestClient.AssertErrorEnvelopeAsync(response, StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task Login_IsRateLimited_AfterFiveAttemptsPerMinute()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var statuses = new List<int>();
        for (var attempt = 0; attempt < 6; attempt++)
        {
            var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
            {
                email = $"forca-{Guid.NewGuid():N}@teste.com",
                password = "SenhaErrada123"
            });
            statuses.Add((int)response.StatusCode);
        }

        Assert.All(statuses.Take(5), status => Assert.Equal(StatusCodes.Status401Unauthorized, status));
        Assert.Equal(StatusCodes.Status429TooManyRequests, statuses[5]);

        using var json = await TestClient.ReadJsonAsync(
            await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "x@y.com", password = "z" }));
        Assert.Equal(429, json.RootElement.GetProperty("status").GetInt32());
    }
}

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace PizzariaLucrativa.Api.Tests;

/// <summary>Helpers HTTP dos testes de integração.</summary>
internal static class TestClient
{
    public sealed record Session(HttpClient Client, string AccessToken, string RefreshToken, int ExpiresIn);

    public static async Task<Session> RegisterAsync(HttpClient client, string pizzeriaName = "Pizzaria Teste")
    {
        var email = $"dono-{Guid.NewGuid():N}@teste.com";
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            password = "Senha123",
            fullName = "Dono da Pizzaria",
            pizzeriaName
        });

        response.EnsureSuccessStatusCode();

        using var json = await ReadJsonAsync(response);
        var root = json.RootElement;

        return new Session(
            client,
            root.GetProperty("accessToken").GetString()!,
            root.GetProperty("refreshToken").GetString()!,
            root.GetProperty("expiresIn").GetInt32());
    }

    public static HttpClient WithToken(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(body);
    }

    public static async Task<T> ReadAsAsync<T>(HttpResponseMessage response)
    {
        var value = await response.Content.ReadFromJsonAsync<T>();
        Assert.NotNull(value);
        return value!;
    }

    public static async Task AssertErrorEnvelopeAsync(HttpResponseMessage response, int status)
    {
        Assert.Equal(status, (int)response.StatusCode);

        using var json = await ReadJsonAsync(response);
        var root = json.RootElement;

        Assert.Equal(status, root.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("message").GetString()));
        Assert.Equal(JsonValueKind.Array, root.GetProperty("errors").ValueKind);
    }
}

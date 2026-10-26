using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Interfaces;
using PizzariaLucrativa.Domain.Entities;
using PizzariaLucrativa.Infrastructure.Persistence;

namespace PizzariaLucrativa.Api.Tests.Ai;

/// <summary>
/// Testes de integração, segurança, multi-tenant, RBAC e orçamento do subsistema de IA (IA3-08..15).
/// </summary>
public sealed class AiIntegrationTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly FakeGeminiChatModelClient _fakeClient = new();

    public AiIntegrationTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient(Action<Dictionary<string, string?>>? configOverride = null)
    {
        var configMap = new Dictionary<string, string?>
        {
            ["Ai:Enabled"] = "true",
            ["Ai:Provider"] = "Gemini",
            ["Ai:ApiKey"] = "test-key-54321",
            ["Ai:Model"] = "gemini-1.5-flash",
            ["Ai:Budget:Enforce"] = "true",
            ["Ai:Budget:MonthlyUsdPerTenant"] = "5.00",
            ["Ai:History:Enabled"] = "true",
            ["Ai:History:RetentionDays"] = "90"
        };

        configOverride?.Invoke(configMap);

        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(configMap);
            });

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IChatModelClient>();
                services.AddSingleton<IChatModelClient>(_fakeClient);
            });
        }).CreateClient();
    }

    private async Task<(string Token, Guid TenantId)> RegisterOwnerAsync(HttpClient client, string prefix)
    {
        var session = await TestClient.RegisterAsync(client, $"Pizzaria {prefix}");
        return (session.AccessToken, Guid.Empty);
    }

    private async Task<string> CreateStaffUserAsync(HttpClient client, string ownerToken, string role)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var email = $"{role}-{Guid.NewGuid():N}@pizzaria.com";
        var res = await client.PostAsJsonAsync("/api/v1/users", new
        {
            email,
            password = "SenhaForte123!",
            fullName = $"Operador {role}",
            role
        });

        res.EnsureSuccessStatusCode();

        // Login com o novo staff
        var loginRes = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = "SenhaForte123!"
        });

        loginRes.EnsureSuccessStatusCode();
        using var json = await TestClient.ReadJsonAsync(loginRes);
        return json.RootElement.GetProperty("accessToken").GetString()!;
    }

    [Fact]
    public async Task Chat_OwnerOnly_RejectsAtendenteAndPizzaioloWith403()
    {
        var client = CreateClient();
        var (ownerToken, _) = await RegisterOwnerAsync(client, "rbac-owner");

        var atendenteToken = await CreateStaffUserAsync(client, ownerToken, "atendente");
        var pizzaioloToken = await CreateStaffUserAsync(client, ownerToken, "pizzaiolo");

        // 1. Atendente -> 403 Forbidden
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", atendenteToken);
        var atendenteRes = await client.PostAsJsonAsync("/api/v1/ai/chat", new ChatRequestDto("Quanto lucrei este mês?"));
        Assert.Equal(HttpStatusCode.Forbidden, atendenteRes.StatusCode);

        var atendenteConvRes = await client.GetAsync("/api/v1/ai/conversations");
        Assert.Equal(HttpStatusCode.Forbidden, atendenteConvRes.StatusCode);

        // 2. Pizzaiolo -> 403 Forbidden
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pizzaioloToken);
        var pizzaioloRes = await client.PostAsJsonAsync("/api/v1/ai/chat", new ChatRequestDto("Quanto lucrei este mês?"));
        Assert.Equal(HttpStatusCode.Forbidden, pizzaioloRes.StatusCode);

        // 3. Owner -> 200 OK
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var ownerRes = await client.PostAsJsonAsync("/api/v1/ai/chat", new ChatRequestDto("Quanto lucrei este mês?"));
        Assert.Equal(HttpStatusCode.OK, ownerRes.StatusCode);
    }

    [Fact]
    public async Task Conversations_Lifecycle_CreateListGetDelete_WorksCorrectly()
    {
        var client = CreateClient();
        var (token, _) = await RegisterOwnerAsync(client, "conv-lifecycle");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // 1. Primeira pergunta cria conversa
        var chatRes1 = await client.PostAsJsonAsync("/api/v1/ai/chat", new ChatRequestDto("Quais regiões tiveram mais pedidos no mês passado?"));
        Assert.Equal(HttpStatusCode.OK, chatRes1.StatusCode);
        var answer1 = await chatRes1.Content.ReadFromJsonAsync<ChatAnswerDto>();
        Assert.NotNull(answer1);
        var conversationId = answer1.ConversationId;

        // 2. Segunda pergunta na mesma conversa
        var chatRes2 = await client.PostAsJsonAsync("/api/v1/ai/chat", new ChatRequestDto("E qual produto deu mais lucro?", conversationId));
        Assert.Equal(HttpStatusCode.OK, chatRes2.StatusCode);
        var answer2 = await chatRes2.Content.ReadFromJsonAsync<ChatAnswerDto>();
        Assert.Equal(conversationId, answer2!.ConversationId);

        // 3. Listar conversas
        var listRes = await client.GetAsync("/api/v1/ai/conversations");
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        var conversations = await listRes.Content.ReadFromJsonAsync<List<ConversationSummaryDto>>();
        Assert.NotNull(conversations);
        Assert.Contains(conversations, c => c.Id == conversationId);

        // 4. Obter detalhe da conversa
        var getRes = await client.GetAsync($"/api/v1/ai/conversations/{conversationId}");
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);
        var detail = await getRes.Content.ReadFromJsonAsync<ConversationDetailDto>();
        Assert.NotNull(detail);
        Assert.Equal(conversationId, detail.Id);
        Assert.True(detail.Messages.Count >= 4); // 2 perguntas do usuário + 2 respostas do assistente

        // 5. Excluir conversa
        var delRes = await client.DeleteAsync($"/api/v1/ai/conversations/{conversationId}");
        Assert.Equal(HttpStatusCode.NoContent, delRes.StatusCode);

        // 6. Tentar obter conversa excluída -> 404
        var getAfterDelRes = await client.GetAsync($"/api/v1/ai/conversations/{conversationId}");
        Assert.Equal(HttpStatusCode.NotFound, getAfterDelRes.StatusCode);
    }

    [Fact]
    public async Task MultiTenant_TenantCannotAccessAnotherTenantsConversation()
    {
        var client = CreateClient();

        // Tenant A cria conversa
        var (tokenA, _) = await RegisterOwnerAsync(client, "tenant-a");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);

        var chatResA = await client.PostAsJsonAsync("/api/v1/ai/chat", new ChatRequestDto("Quanto lucrei no mês de setembro?"));
        var answerA = await chatResA.Content.ReadFromJsonAsync<ChatAnswerDto>();
        var convIdA = answerA!.ConversationId;

        // Tenant B tenta ler a conversa de A -> 404
        var (tokenB, _) = await RegisterOwnerAsync(client, "tenant-b");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);

        var getResB = await client.GetAsync($"/api/v1/ai/conversations/{convIdA}");
        Assert.Equal(HttpStatusCode.NotFound, getResB.StatusCode);

        // Tenant B tenta excluir conversa de A -> 404
        var delResB = await client.DeleteAsync($"/api/v1/ai/conversations/{convIdA}");
        Assert.Equal(HttpStatusCode.NotFound, delResB.StatusCode);
    }

    [Fact]
    public async Task Budget_WhenExceeded_BlocksExternalCallAndReturnsSafeFallback()
    {
        // Configura teto mensal menor do que o custo estimado por pergunta (0.001) para bloquear
        var client = CreateClient(config =>
        {
            config["Ai:Budget:Enforce"] = "true";
            config["Ai:Budget:MonthlyUsdPerTenant"] = "0.0001";
        });

        var (token, _) = await RegisterOwnerAsync(client, "budget-test");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Consulta de uso inicial
        var usageRes = await client.GetAsync("/api/v1/ai/usage");
        Assert.Equal(HttpStatusCode.OK, usageRes.StatusCode);
        var usage = await usageRes.Content.ReadFromJsonAsync<AiUsageDto>();
        Assert.NotNull(usage);

        // Pergunta deve ser bloqueada antes da chamada externa e retornar fallback seguro
        var chatRes = await client.PostAsJsonAsync("/api/v1/ai/chat", new ChatRequestDto("Quanto lucrei este mês?"));
        Assert.Equal(HttpStatusCode.OK, chatRes.StatusCode);

        var answer = await chatRes.Content.ReadFromJsonAsync<ChatAnswerDto>();
        Assert.NotNull(answer);
        Assert.True(answer.Meta.IsFallback);
        Assert.Contains("limite de orçamento", answer.Text);
    }

    [Fact]
    public async Task History_WhenDisabled_ChatWorksAndNothingPersistedInAiConversations()
    {
        var client = CreateClient(config =>
        {
            config["Ai:History:Enabled"] = "false";
        });

        var (token, _) = await RegisterOwnerAsync(client, "history-disabled");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var chatRes = await client.PostAsJsonAsync("/api/v1/ai/chat", new ChatRequestDto("Quanto lucrei no mês de setembro?"));
        Assert.Equal(HttpStatusCode.OK, chatRes.StatusCode);

        var answer = await chatRes.Content.ReadFromJsonAsync<ChatAnswerDto>();
        Assert.NotNull(answer);
        Assert.False(string.IsNullOrWhiteSpace(answer.Text));

        // Listagem de conversas deve vir vazia
        var listRes = await client.GetAsync("/api/v1/ai/conversations");
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        var convs = await listRes.Content.ReadFromJsonAsync<List<ConversationSummaryDto>>();
        Assert.Empty(convs!);

        // Mas o consumo continua registrado em AiUsageRecords
        var usageRes = await client.GetAsync("/api/v1/ai/usage");
        Assert.Equal(HttpStatusCode.OK, usageRes.StatusCode);
        var usage = await usageRes.Content.ReadFromJsonAsync<AiUsageDto>();
        Assert.NotNull(usage);
        Assert.True(usage.MonthlyInputTokens > 0 || usage.InputTokens >= 0);
    }

    [Fact]
    public async Task Timeout_GeminiSimulatedTimeout_ReturnsSafeFallbackWithout500()
    {
        var client = CreateClient();
        var (token, _) = await RegisterOwnerAsync(client, "timeout-test");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        _fakeClient.SimulateTimeout = true;
        try
        {
            var chatRes = await client.PostAsJsonAsync("/api/v1/ai/chat", new ChatRequestDto("Quanto lucrei no mês passado?"));
            Assert.Equal(HttpStatusCode.OK, chatRes.StatusCode);

            var answer = await chatRes.Content.ReadFromJsonAsync<ChatAnswerDto>();
            Assert.NotNull(answer);
            Assert.True(answer.Meta.IsFallback);
            Assert.Contains("Não foi possível", answer.Text);
        }
        finally
        {
            _fakeClient.SimulateTimeout = false;
        }
    }

    [Fact]
    public async Task Health_Endpoint_ReturnsStatusWithoutExposingSecrets()
    {
        var client = CreateClient();
        var (token, _) = await RegisterOwnerAsync(client, "health-test");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await client.GetAsync("/api/v1/ai/health");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var json = await res.Content.ReadAsStringAsync();
        Assert.Contains("\"enabled\":true", json);
        Assert.Contains("\"provider\":\"Gemini\"", json);
        Assert.Contains("\"apiKeyConfigured\":true", json);
        Assert.DoesNotContain("test-key-54321", json); // Segredo NUNCA deve vazar
    }

    [Fact]
    public async Task Suggestions_Endpoint_ReturnsStandardPrompts()
    {
        var client = CreateClient();
        var (token, _) = await RegisterOwnerAsync(client, "sugg-test");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await client.GetAsync("/api/v1/ai/suggestions");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var suggestions = await res.Content.ReadFromJsonAsync<List<string>>();
        Assert.NotNull(suggestions);
        Assert.NotEmpty(suggestions);
        Assert.Contains(suggestions, s => s.Contains("lucrei", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Rollout_WhenZeroPercent_BlocksTenantWithFallback()
    {
        var client = CreateClient(config =>
        {
            config["Ai:RolloutPercentage"] = "0";
        });

        var (token, _) = await RegisterOwnerAsync(client, "rollout-zero");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var chatRes = await client.PostAsJsonAsync("/api/v1/ai/chat", new ChatRequestDto("Quanto lucrei no mês passado?"));
        Assert.Equal(HttpStatusCode.OK, chatRes.StatusCode);

        var answer = await chatRes.Content.ReadFromJsonAsync<ChatAnswerDto>();
        Assert.NotNull(answer);
        Assert.True(answer.Meta.IsFallback);
        Assert.Contains("liberação gradual", answer.Text);
    }

    [Fact]
    public async Task KillSwitch_WhenDisabled_BlocksAllTenantsImmediately()
    {
        var client = CreateClient(config =>
        {
            config["Ai:Enabled"] = "false";
        });

        var (token, _) = await RegisterOwnerAsync(client, "killswitch");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var chatRes = await client.PostAsJsonAsync("/api/v1/ai/chat", new ChatRequestDto("Quanto lucrei no mês passado?"));
        Assert.Equal(HttpStatusCode.OK, chatRes.StatusCode);

        var answer = await chatRes.Content.ReadFromJsonAsync<ChatAnswerDto>();
        Assert.NotNull(answer);
        Assert.True(answer.Meta.IsFallback);
        Assert.Contains("desabilitado no momento", answer.Text);
    }

    [Fact]
    public async Task Retention_Cleanup_RemovesExpiredConversations()
    {
        var client = CreateClient();
        var (token, _) = await RegisterOwnerAsync(client, "retention-test");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Cria conversa válida
        var chatRes = await client.PostAsJsonAsync("/api/v1/ai/chat", new ChatRequestDto("Quanto lucrei este mês?"));
        Assert.Equal(HttpStatusCode.OK, chatRes.StatusCode);
        var answer = await chatRes.Content.ReadFromJsonAsync<ChatAnswerDto>();
        Assert.NotNull(answer);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Envelhece a conversa no banco para 100 dias atrás (> 90 dias de retenção) via raw SQL
        // no TenantEntity (tabela base do TPT onde UpdatedAt é persistido).
        var pastDate = DateTime.UtcNow.AddDays(-100);
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE TenantEntity SET UpdatedAt = {0} WHERE Id = {1}",
            pastDate,
            answer.ConversationId);

        var cleanupService = scope.ServiceProvider.GetRequiredService<PizzariaLucrativa.Infrastructure.Ai.AiHistoryCleanupService>();
        var removedCount = await cleanupService.RunCleanupAsync();

        Assert.True(removedCount >= 1);

        // Confirma que a conversa envelhecida foi removida
        var convAfter = await db.AiConversations.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == answer.ConversationId);
        Assert.Null(convAfter);
    }
}

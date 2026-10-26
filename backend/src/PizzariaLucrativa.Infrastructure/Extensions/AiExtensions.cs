using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PizzariaLucrativa.Application.Interfaces;
using PizzariaLucrativa.Application.Options;
using PizzariaLucrativa.Infrastructure.Ai;

namespace PizzariaLucrativa.Infrastructure.Extensions;

public static class AiExtensions
{
    public static IServiceCollection AddInfrastructureAi(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var aiSection = configuration.GetSection(AiOptions.SectionName);
        services.Configure<AiOptions>(aiSection);

        // Validação no startup
        var initialOptions = aiSection.Get<AiOptions>() ?? new AiOptions();
        initialOptions.Validate();

        services.AddHttpClient<IChatModelClient, GeminiChatModelClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<AiOptions>>().Value;
            if (!string.IsNullOrWhiteSpace(options.BaseUrl))
            {
                client.BaseAddress = new Uri(options.BaseUrl);
            }
            client.Timeout = TimeSpan.FromSeconds(Math.Max(options.TimeoutSeconds + 5, 30));
        });

        services.AddScoped<IAiConversationStore, AiConversationStore>();
        services.AddSingleton<AiHistoryCleanupService>();
        services.AddHostedService(sp => sp.GetRequiredService<AiHistoryCleanupService>());

        return services;
    }
}

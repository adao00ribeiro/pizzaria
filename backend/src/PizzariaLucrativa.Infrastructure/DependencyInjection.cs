using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PizzariaLucrativa.Infrastructure.Extensions;

namespace PizzariaLucrativa.Infrastructure;

/// <summary>Registro agregador das dependências de infraestrutura decompostas por responsabilidade única (SRP).</summary>
public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddInfrastructurePersistence(configuration)
            .AddInfrastructureIdentity(configuration)
            .AddInfrastructureQueues()
            .AddInfrastructureAi(configuration);

        return services;
    }
}

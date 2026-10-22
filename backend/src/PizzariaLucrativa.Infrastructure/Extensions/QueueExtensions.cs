using Microsoft.Extensions.DependencyInjection;
using PizzariaLucrativa.Application.Interfaces;
using PizzariaLucrativa.Infrastructure.Queues;

namespace PizzariaLucrativa.Infrastructure.Extensions;

/// <summary>Extensões de registro de filas em memória e hosted services em background.</summary>
public static class QueueExtensions
{
    public static IServiceCollection AddInfrastructureQueues(this IServiceCollection services)
    {
        // Fila de recálculo em cadeia (docs/04 §5)
        services.AddSingleton<RecalculationQueue>();
        services.AddSingleton<IRecalculationQueue>(sp => sp.GetRequiredService<RecalculationQueue>());
        services.AddHostedService<RecalculationWorker>();

        return services;
    }
}

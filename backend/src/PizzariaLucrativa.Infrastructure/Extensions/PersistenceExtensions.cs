using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PizzariaLucrativa.Application.Interfaces;
using PizzariaLucrativa.Infrastructure.Identity;
using PizzariaLucrativa.Infrastructure.Persistence;

namespace PizzariaLucrativa.Infrastructure.Extensions;

/// <summary>Extensões de registro da persistência e banco de dados do EF Core (SQLite / SQL Server).</summary>
public static class PersistenceExtensions
{
    public static IServiceCollection AddInfrastructurePersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ---- Banco (ADR-6: SQLite em dev, SQL Server em produção) ----------
        var provider = configuration["Database:Provider"] ?? "Sqlite";
        var connectionString = configuration.GetConnectionString("DefaultConnection")
                               ?? "Data Source=pizzaria-lucrativa.db";

        services.AddDbContext<AppDbContext>(options =>
        {
            if (string.Equals(provider, "SqlServer", StringComparison.OrdinalIgnoreCase))
                options.UseSqlServer(connectionString);
            else
                options.UseSqlite(connectionString);
        });

        // A Application enxerga o DbContext só pela abstração (sem referência a EF Core).
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        // Singleton: o valor vive em AsyncLocal (fluxo por request/worker), não na instância.
        services.AddSingleton<ITenantProvider, TenantProvider>();

        // Seeders de banco (docs/10, docs/09).
        services.AddScoped<RoleSeeder>();
        services.AddScoped<DemoSeeder>();

        return services;
    }
}

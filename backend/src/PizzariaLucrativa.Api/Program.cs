using PizzariaLucrativa.Api.Extensions;
using PizzariaLucrativa.Application;
using PizzariaLucrativa.Infrastructure;
using PizzariaLucrativa.Infrastructure.Persistence;
using Serilog;
using Serilog.Events;

// Serilog desde o bootstrap (NF-N04: logs estruturados).
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    // ---- Camadas da aplicação ---------------------------------------------
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    // ---- API: Contexto, Versionamento, Controllers, Swagger, JWT, RBAC, RateLimiting e CORS ----
    builder.Services
        .AddApiCurrentUser()
        .AddApiVersioningConfiguration()
        .AddApiControllers()
        .AddApiSwagger()
        .AddApiJwtAuthentication(builder.Configuration)
        .AddApiAuthorizationPolicies()
        .AddApiRateLimiting()
        .AddApiCors(builder.Configuration);

    var app = builder.Build();

    // ---- Pipeline HTTP -----------------------------------------------------
    app.UseApiPipeline();

    await DatabaseInitializer.InitializeDatabaseAsync(
        app.Services,
        app.Configuration.GetValue<bool>("Seed:Demo"));

    app.Run();
}
catch (Exception exception)
{
    Log.Fatal(exception, "A API falhou ao iniciar.");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

/// <summary>Expõe o entry point para os testes de integração (WebApplicationFactory).</summary>
public partial class Program;

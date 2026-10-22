namespace PizzariaLucrativa.Api.Extensions;

/// <summary>Extensões de registro da política CORS para o frontend.</summary>
public static class CorsExtensions
{
    public static IServiceCollection AddApiCors(this IServiceCollection services, IConfiguration configuration)
    {
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        services.AddCors(options => options.AddPolicy("frontend", policy =>
        {
            if (allowedOrigins.Length == 0)
                policy.AllowAnyOrigin();
            else
                policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
        }));

        return services;
    }
}

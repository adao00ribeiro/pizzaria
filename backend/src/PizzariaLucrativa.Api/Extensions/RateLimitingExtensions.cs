using System.Threading.RateLimiting;

namespace PizzariaLucrativa.Api.Extensions;

/// <summary>Extensões de registro de Rate Limiting por IP para endpoints de login e cardápio público.</summary>
public static class RateLimitingExtensions
{
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                var response = context.HttpContext.Response;
                response.ContentType = "application/json";
                await response.WriteAsync(
                    """{"status":429,"message":"Muitas tentativas de login. Aguarde 1 minuto.","errors":[]}""",
                    cancellationToken);
            };
            options.AddPolicy("login", context =>
            {
                var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1),
                    AutoReplenishment = true,
                    QueueLimit = 0
                });
            });

            // Sprint 5: rate limit dedicado nos endpoints públicos (10 pedidos/min e 30 consultas/min por IP).
            options.AddPolicy("public-orders", context =>
            {
                var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter($"pub-ord:{ip}", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    AutoReplenishment = true,
                    QueueLimit = 0
                });
            });

            options.AddPolicy("public-tracking", context =>
            {
                var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter($"pub-trk:{ip}", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 30,
                    Window = TimeSpan.FromMinutes(1),
                    AutoReplenishment = true,
                    QueueLimit = 0
                });
            });

            // Sprint IA-3: rate limit dedicado para chat da IA (20 mensagens/min por IP/usuário)
            options.AddPolicy("ai-chat", context =>
            {
                var key = context.User?.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter($"ai-chat:{key}", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 20,
                    Window = TimeSpan.FromMinutes(1),
                    AutoReplenishment = true,
                    QueueLimit = 0
                });
            });
        });

        return services;
    }
}

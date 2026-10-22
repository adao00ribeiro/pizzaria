using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using PizzariaLucrativa.Api.Middleware;
using Serilog;

namespace PizzariaLucrativa.Api.Extensions;

/// <summary>Extensões de orquestração do pipeline HTTP de middlewares e endpoints da API.</summary>
public static class PipelineExtensions
{
    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        // O request logging fica por fora do handler de exceções para registrar o
        // status final (400/409/500) já convertido.
        app.UseSerilogRequestLogging();
        app.UseMiddleware<ExceptionMiddleware>();
        app.UseApiStatusCodePages();

        if (app.Environment.IsDevelopment())
        {
            app.UseApiSwagger();
        }

        app.UseCors("frontend");
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseMiddleware<TenantContextMiddleware>();
        app.UseAuthorization();

        app.MapControllers();

        return app;
    }
}

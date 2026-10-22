using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace PizzariaLucrativa.Api.Extensions;

/// <summary>Extensões de configuração de respostas JSON padronizadas para códigos de status HTTP.</summary>
public static class StatusCodePagesExtensions
{
    public static IApplicationBuilder UseApiStatusCodePages(this IApplicationBuilder app)
    {
        // Respostas com corpo vazio (404/401/405...) ganham o formato padrão da API.
        return app.UseStatusCodePages(async context =>
        {
            var response = context.HttpContext.Response;
            if (response.HasStarted || response.ContentType is not null)
                return;

            response.ContentType = "application/json";
            var status = response.StatusCode;
            var message = status switch
            {
                401 => "Não autenticado.",
                403 => "Acesso negado.",
                404 => "Recurso não encontrado.",
                405 => "Método não permitido.",
                _ => "Ocorreu um erro."
            };
            await response.WriteAsync($$"""{"status":{{status}},"message":"{{message}}","errors":[]}""");
        });
    }
}

using Microsoft.Extensions.DependencyInjection;
using PizzariaLucrativa.Api.Identity;
using PizzariaLucrativa.Application.Interfaces;

namespace PizzariaLucrativa.Api.Extensions;

/// <summary>Extensões de registro do contexto do usuário atual da requisição.</summary>
public static class CurrentUserExtensions
{
    public static IServiceCollection AddApiCurrentUser(this IServiceCollection services)
    {
        // Usuário do request (grava Order.PlacedByUserId) — docs/10, Sprint 2.
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

        return services;
    }
}

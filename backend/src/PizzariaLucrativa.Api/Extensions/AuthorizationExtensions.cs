using System.Security.Claims;
using PizzariaLucrativa.Infrastructure.Persistence;

namespace PizzariaLucrativa.Api.Extensions;

/// <summary>Extensões de registro das políticas de autorização RBAC (OwnerOnly, AtendenteOuOwner, ProducaoOuOwner).</summary>
public static class AuthorizationExtensions
{
    public static IServiceCollection AddApiAuthorizationPolicies(this IServiceCollection services)
    {
        // Papéis: owner / atendente / pizzaiolo / garcom (premissa P4). A verificação cobre tanto
        // ClaimTypes.Role (mapeamento inbound do JwtBearer) quanto a claim crua "role".
        services.AddAuthorization(options =>
        {
            static bool IsInRole(ClaimsPrincipal user, params string[] roles)
            {
                if (roles.Any(user.IsInRole))
                    return true;

                var rawRoles = user.FindAll("role").Select(c => c.Value).ToArray();
                return roles.Any(r => rawRoles.Contains(r, StringComparer.OrdinalIgnoreCase));
            }

            options.AddPolicy("OwnerOnly", policy =>
                policy.RequireAssertion(context => IsInRole(context.User, AppRoles.Owner)));

            options.AddPolicy("AtendenteOuOwner", policy =>
                policy.RequireAssertion(context => IsInRole(context.User, AppRoles.Atendente, AppRoles.Garcom, AppRoles.Owner)));

            options.AddPolicy("ProducaoOuOwner", policy =>
                policy.RequireAssertion(context => IsInRole(context.User, AppRoles.Pizzaiolo, AppRoles.Owner)));
        });

        return services;
    }
}

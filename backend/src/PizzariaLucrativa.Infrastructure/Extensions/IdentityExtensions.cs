using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PizzariaLucrativa.Application.Interfaces;
using PizzariaLucrativa.Infrastructure.Identity;
using PizzariaLucrativa.Infrastructure.Persistence;

namespace PizzariaLucrativa.Infrastructure.Extensions;

/// <summary>Extensões de registro do ASP.NET Core Identity, JWT Options e serviços de autenticação de infraestrutura.</summary>
public static class IdentityExtensions
{
    public static IServiceCollection AddInfrastructureIdentity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddIdentityCore<AppUser>(options =>
            {
                options.Password.RequiredLength = 6;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>();

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserAdminService, UserAdminService>();

        return services;
    }
}

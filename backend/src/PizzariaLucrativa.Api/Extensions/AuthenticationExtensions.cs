using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using PizzariaLucrativa.Infrastructure.Identity;

namespace PizzariaLucrativa.Api.Extensions;

/// <summary>Extensões de registro da autenticação JWT Bearer da API.</summary>
public static class AuthenticationExtensions
{
    public static IServiceCollection AddApiJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtSection = configuration.GetSection(JwtOptions.SectionName);
        var jwtKey = jwtSection["Key"]
                     ?? throw new InvalidOperationException("Configuração \"Jwt:Key\" ausente.");

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtSection["Issuer"],
                    ValidateAudience = true,
                    ValidAudience = jwtSection["Audience"],
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };

                options.Events = new JwtBearerEvents
                {
                    OnChallenge = context =>
                    {
                        // HandleResponse impede o corpo vazio do handler; o 401 (definido aqui)
                        // é preenchido pelas StatusCodePages no formato padrão da API.
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return Task.CompletedTask;
                    }
                };
            });

        return services;
    }
}

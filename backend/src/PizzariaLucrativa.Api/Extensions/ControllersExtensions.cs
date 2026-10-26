using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using PizzariaLucrativa.Api.Filters;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Application.Serialization;

namespace PizzariaLucrativa.Api.Extensions;

/// <summary>Extensões de registro de Controllers MVC, filtros de validação e serialização JSON.</summary>
public static class ControllersExtensions
{
    public static IServiceCollection AddApiControllers(this IServiceCollection services)
    {
        services
            .AddControllers(options => options.Filters.Add<ValidationFilter>())
            .AddJsonOptions(options =>
            {
                // Contrato de IA: estes enums saem em minúsculas (bar, line, currency...)
                // antes do conversor global de enums (PascalCase).
                options.JsonSerializerOptions.Converters.Add(new LowerCaseEnumConverter<AnswerBlockType>());
                options.JsonSerializerOptions.Converters.Add(new LowerCaseEnumConverter<ChartKind>());
                options.JsonSerializerOptions.Converters.Add(new LowerCaseEnumConverter<ChartValueFormat>());
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            });

        return services;
    }
}

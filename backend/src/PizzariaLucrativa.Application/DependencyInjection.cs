using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using PizzariaLucrativa.Application.Ai;
using PizzariaLucrativa.Application.Interfaces;
using PizzariaLucrativa.Application.Services;
using PizzariaLucrativa.Application.Validators;

namespace PizzariaLucrativa.Application;

/// <summary>Registro dos services e validadores da camada Application.</summary>
public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddValidatorsFromAssemblyContaining<IngredientInputValidator>();

        services.AddScoped<IIngredientService, IngredientService>();
        services.AddScoped<IPreparationService, PreparationService>();
        services.AddScoped<IFixedCostService, FixedCostService>();
        services.AddScoped<IFeeService, FeeService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ISizeService, SizeService>();
        services.AddScoped<ITechnicalSheetService, TechnicalSheetService>();
        services.AddScoped<IPricingService, PricingService>();
        services.AddScoped<IComboService, ComboService>();
        services.AddScoped<IBeverageService, BeverageService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IAlertService, AlertService>();
        services.AddScoped<IStockService, StockService>();
        services.AddScoped<IStockConsumptionService, StockConsumptionService>();
        services.AddScoped<IStockReplenishmentService, StockReplenishmentService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IKitchenService, KitchenService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<IRecalculationService, RecalculationService>();
        services.AddScoped<INeighborhoodService, NeighborhoodService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        services.AddScoped<IDateTimeProvider, DateTimeProvider>();
        services.AddScoped<IReportPeriodResolver, ReportPeriodResolver>();

        // Subsistema de IA (Sprint IA-3)
        services.AddSingleton<AiToolRegistry>();
        services.AddScoped<AiToolExecutor>();
        services.AddSingleton<AiPromptBuilder>();
        services.AddScoped<IAiUsageService, AiUsageService>();
        services.AddScoped<IAiChatService, AiChatService>();

        return services;
    }
}

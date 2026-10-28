using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PizzariaLucrativa.Domain.Services;

namespace PizzariaLucrativa.Infrastructure.Persistence;

/// <summary>
/// Inicialização do banco de dados no startup: aplica migrations (SQLite) ou cria o schema,
/// executa o seed idempotente de papéis, o backfill de slugs públicos e o seed de demonstração.
/// </summary>
public static class DatabaseInitializer
{
    public static async Task InitializeDatabaseAsync(IServiceProvider services, bool seedDemo)
    {
        using var scope = services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("DatabaseInitializer");
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (db.Database.IsSqlite() && db.Database.GetMigrations().Any())
            await db.Database.MigrateAsync();
        else
            await db.Database.EnsureCreatedAsync();

        logger.LogInformation("Banco de dados pronto ({Provider}).", db.Database.ProviderName);

        // Seed idempotente dos papéis funcionais (docs/10, Sprint 1).
        var roleSeeder = scope.ServiceProvider.GetRequiredService<RoleSeeder>();
        await roleSeeder.SeedAsync();

        // Backfill do slug público em tenants antigos (docs/10, Sprint 1).
        await BackfillTenantSlugsAsync(db, logger);

        // Garante insumos de estoque para bebidas que não possuem (docs/10, Sprint 2).
        await EnsureBeverageStockIngredientsAsync(db, logger);

        if (seedDemo)
        {
            var seeder = scope.ServiceProvider.GetRequiredService<DemoSeeder>();
            var created = await seeder.SeedAsync();
            logger.LogInformation(
                created
                    ? "Seed de demonstração criado (login {Email} / {Password})."
                    : "Seed de demonstração já presente — nada a criar.",
                DemoSeeder.DemoEmail,
                DemoSeeder.DemoPassword);
        }
    }

    /// <summary>
    /// Atribui um slug único a tenants criados antes da coluna existir (docs/10, Sprint 1).
    /// Idempotente: só mexe em registros com Slug nulo/vazio.
    /// </summary>
    private static async Task BackfillTenantSlugsAsync(AppDbContext db, ILogger logger)
    {
        var tenants = await db.Tenants
            .Where(t => t.Slug == null || t.Slug == "")
            .ToListAsync();

        if (tenants.Count == 0)
            return;

        var used = new HashSet<string>(
            await db.Tenants
                .Where(t => t.Slug != null && t.Slug != "")
                .Select(t => t.Slug!)
                .ToListAsync(),
            StringComparer.OrdinalIgnoreCase);

        foreach (var tenant in tenants)
        {
            var baseSlug = SlugGenerator.Generate(tenant.Name);
            if (string.IsNullOrWhiteSpace(baseSlug))
                baseSlug = $"pizzaria-{tenant.Id.ToString("N")[..8]}";

            var slug = baseSlug;
            var suffix = 2;
            while (used.Contains(slug))
            {
                slug = SlugGenerator.WithSuffix(baseSlug, suffix);
                suffix++;
            }

            used.Add(slug);
            tenant.Slug = slug;
            tenant.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Slug público preenchido em {Count} pizzaria(s).", tenants.Count);
    }

    /// <summary>
    /// Garante que todas as bebidas cadastradas possuam insumo vinculado para controle e baixa de estoque.
    /// </summary>
    private static async Task EnsureBeverageStockIngredientsAsync(AppDbContext db, ILogger logger)
    {
        var beveragesWithoutIngredient = await db.Products
            .Where(p => p.Category == PizzariaLucrativa.Domain.Enums.ProductCategory.Beverage && p.IngredientId == null)
            .ToListAsync();

        if (beveragesWithoutIngredient.Count == 0)
            return;

        foreach (var bev in beveragesWithoutIngredient)
        {
            var ingredient = new PizzariaLucrativa.Domain.Entities.Ingredient
            {
                TenantId = bev.TenantId,
                Name = bev.Name,
                PurchaseUnit = PizzariaLucrativa.Domain.Enums.MeasureUnit.un,
                PurchaseQuantity = 1,
                PurchasePrice = bev.PurchasePrice ?? 0m,
                UnitCost = bev.PurchasePrice ?? 0m,
                LossPercentage = 0,
                CurrentStock = 12m,
                MinimumStock = 5m,
                MarkupMultiplier = bev.MarkupMultiplier ?? 2.0m
            };
            db.Ingredients.Add(ingredient);
            await db.SaveChangesAsync();
            bev.IngredientId = ingredient.Id;
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Vinculados {Count} insumos de estoque a bebidas existentes.", beveragesWithoutIngredient.Count);
    }
}

using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PizzariaLucrativa.Application.Interfaces;
using PizzariaLucrativa.Domain.Entities;

namespace PizzariaLucrativa.Infrastructure.Persistence;

/// <summary>
/// DbContext da aplicação: tabelas Identity + modelo de negócio do tenant.
/// Isolamento multi-tenant (NF-S03):
///  - hierarquia TPT (uma tabela por entidade, colunas comuns na tabela base);
///  - query filter global em TenantEntity filtrando pelo claim do token;
///  - TenantId carimbado no SaveChanges quando ausente.
/// SQLite (dev) recebe converters para decimal/DateTimeOffset porque o provider
/// não ordena nem compara decimal nativamente (ADR-6).
/// </summary>
public class AppDbContext : IdentityDbContext<AppUser>, IAppDbContext
{
    private readonly ITenantProvider _tenantProvider;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantProvider tenantProvider)
        : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<Preparation> Preparations => Set<Preparation>();
    public DbSet<PreparationItem> PreparationItems => Set<PreparationItem>();
    public DbSet<FixedCost> FixedCosts => Set<FixedCost>();
    public DbSet<FeeConfig> FeeConfigs => Set<FeeConfig>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Size> Sizes => Set<Size>();
    public DbSet<TechnicalSheet> TechnicalSheets => Set<TechnicalSheet>();
    public DbSet<TechnicalSheetItem> TechnicalSheetItems => Set<TechnicalSheetItem>();
    public DbSet<Combo> Combos => Set<Combo>();
    public DbSet<ComboItem> ComboItems => Set<ComboItem>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderItemFlavor> OrderItemFlavors => Set<OrderItemFlavor>();
    public DbSet<Neighborhood> Neighborhoods => Set<Neighborhood>();
    public DbSet<AiConversation> AiConversations => Set<AiConversation>();
    public DbSet<AiMessage> AiMessages => Set<AiMessage>();
    public DbSet<AiUsageRecord> AiUsageRecords => Set<AiUsageRecord>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ---- Filtro global de tenant (NF-S03/S04) --------------------------
        // Avaliado por query (funcletize) a cada consulta: null => nenhum registro.
        builder.Entity<TenantEntity>(entity =>
        {
            entity.HasQueryFilter(e => e.TenantId == _tenantProvider.TenantId);
            entity.HasIndex(e => e.TenantId);
            // TPT: CreatedAt vive na tabela base, então o índice declarado nas entidades
            // derivadas (histórico de IA) é materializado aqui como composto com TenantId.
            entity.HasIndex(e => new { e.TenantId, e.CreatedAt });
            entity.Property(e => e.Id).ValueGeneratedNever();
        });

        // ---- Configurações por entidade (IEntityTypeConfiguration<T>) ------
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // ---- Precisão monetária (docs/05: decimal(18,4)) -------------------
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(decimal) || property.ClrType == typeof(decimal?))
                {
                    property.SetPrecision(18);
                    property.SetScale(4);
                }
            }
        }

        ApplySqliteConversions(builder);
    }

    /// <summary>
    /// SQLite (dev) não ordena nem compara decimal e não ordena DateTimeOffset (ADR-6).
    /// Mapeia decimal para inteiro ×10.000 (4 casas — alinhado à política de arredondamento)
    /// e DateTimeOffset para ticks UTC, mantendo ordenação e comparação exatas.
    /// </summary>
    private void ApplySqliteConversions(ModelBuilder builder)
    {
        if (!Database.IsSqlite())
            return;

        var decimalToScaledLong = new ValueConverter<decimal, long>(
            v => (long)Math.Round(v * 10_000m, MidpointRounding.AwayFromZero),
            v => v / 10_000m);

        var nullableDecimalToScaledLong = new ValueConverter<decimal?, long?>(
            v => v == null ? null : (long)Math.Round(v.Value * 10_000m, MidpointRounding.AwayFromZero),
            v => v == null ? null : v / 10_000m);

        var dateTimeOffsetToTicks = new ValueConverter<DateTimeOffset, long>(
            v => v.UtcTicks,
            v => new DateTimeOffset(v, TimeSpan.Zero));

        var nullableDateTimeOffsetToTicks = new ValueConverter<DateTimeOffset?, long?>(
            v => v == null ? null : v.Value.UtcTicks,
            v => v == null ? null : new DateTimeOffset(v.Value, TimeSpan.Zero));

        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(decimal))
                    property.SetValueConverter(decimalToScaledLong);
                else if (property.ClrType == typeof(decimal?))
                    property.SetValueConverter(nullableDecimalToScaledLong);
                else if (property.ClrType == typeof(DateTimeOffset))
                    property.SetValueConverter(dateTimeOffsetToTicks);
                else if (property.ClrType == typeof(DateTimeOffset?))
                    property.SetValueConverter(nullableDateTimeOffsetToTicks);
            }
        }
    }

    /// <summary>Carimba o tenant do token em inserções sem TenantId e atualiza UpdatedAt.</summary>
    private void StampTenant()
    {
        var tenantId = _tenantProvider.TenantId;

        foreach (var entry in ChangeTracker.Entries<TenantEntity>())
        {
            if (entry.State == EntityState.Added && entry.Entity.TenantId == Guid.Empty && tenantId is not null)
                entry.Entity.TenantId = tenantId.Value;

            if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = DateTime.UtcNow;
        }
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampTenant();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampTenant();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    // ---- Implementação explícita de IAppDbContext ----------------------------
    // (DbContext já declina Add/Remove/Set com retornos EntityEntry/DbSet —
    //  os retornos diferem da interface, exigindo implementação explícita.)

    IQueryable<T> IAppDbContext.Set<T>() => Set<T>();

    void IAppDbContext.Add<T>(T entity) => Add(entity);

    void IAppDbContext.AddRange<T>(IEnumerable<T> entities) => AddRange(entities);

    void IAppDbContext.Remove<T>(T entity) => Remove(entity);

    void IAppDbContext.RemoveRange<T>(IEnumerable<T> entities) => RemoveRange(entities);

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        if (Database.CurrentTransaction is not null)
        {
            await action(cancellationToken);
            return;
        }

        await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await action(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public Task<List<T>> ToListAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default)
        => EntityFrameworkQueryableExtensions.ToListAsync(query, cancellationToken);

    public Task<int> CountAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default)
        => EntityFrameworkQueryableExtensions.CountAsync(query, cancellationToken);

    public Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default)
        => EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(query, cancellationToken);
}

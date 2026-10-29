# Persistence Infrastructure

## Overview
Core relational persistence engine implementing Entity Framework Core database context `AppDbContext` (fulfilling `IAppDbContext`), multi-tenant row-level filtering, automatic tenant ID stamping, startup schema migrations, and idempotent database seeders.

## Interface
- `AppDbContext`: Primary EF Core context providing `IAppDbContext` implementation, entity sets, transactional execution, and multi-tenant isolation (`[AppDbContext.cs:AppDbContext]`)
- `AppRoles`: Role definitions (`Owner`, `Atendente`, `Pizzaiolo`, `Garcom`), normalization, and validation utilities (`[AppRoles.cs:AppRoles]`)
- `AppUser`: ASP.NET Core Identity user entity extended with `TenantId`, `FullName`, and `CreatedAt` (`[AppUser.cs:AppUser]`)
- `RefreshToken`: Refresh token persistence entity tracking SHA-256 hash, expiration, and revocation status (`[RefreshToken.cs:RefreshToken]`)
- `DatabaseInitializer`: Coordinates startup database schema migration, role seeding, slug backfilling, and demo data initialization (`[DatabaseInitializer.cs:DatabaseInitializer]`)
- `RoleSeeder`: Idempotently registers application roles into `RoleManager<IdentityRole>` (`[RoleSeeder.cs:RoleSeeder]`)
- `DemoSeeder`: Provisions a comprehensive mock pizzeria with preconfigured products, technical sheets, stock, staff, and simulated orders (`[DemoSeeder.cs:DemoSeeder]`)
- Subdirectories:
  - [Mappings](./Mappings/README.md): Entity Framework Core Fluent API mapping configurations for all database entities.
  - [Migrations](./Migrations/README.md): Schema version migrations and model snapshot history.

## Invariants
- Multi-tenant query isolation: Global query filter on `TenantEntity` ensures queries match `_tenantProvider.TenantId` (`[AppDbContext.cs:OnModelCreating]`)
- Automatic tenant injection: `StampTenant` assigns `TenantId` from the current ambient context to newly added `TenantEntity` instances (`[AppDbContext.cs:StampTenant]`)
- Audit tracking: Automatically updates `UpdatedAt` timestamp on modified `TenantEntity` instances upon `SaveChanges` (`[AppDbContext.cs:StampTenant]`)
- Monetary precision: Enforces `decimal(18,4)` precision across all decimal entity properties (`[AppDbContext.cs:OnModelCreating]`)
- SQLite compatibility layer: Automatically converts decimals to scaled integer values and `DateTimeOffset` to UTC ticks when running against SQLite (`[AppDbContext.cs:ApplySqliteConversions]`)

## Side Effects
- Modifies database schema through EF Core migrations and `EnsureCreatedAsync` (`[DatabaseInitializer.cs:InitializeDatabaseAsync]`)
- Commits transactions and mutates rows across all entity tables (`[AppDbContext.cs:SaveChangesAsync]`, `[AppDbContext.cs:ExecuteInTransactionAsync]`)

## Verification
- Run: `dotnet test backend/`

# Infrastructure Extensions

## Overview
Modular dependency injection extensions that partition the registration of infrastructure services across AI, Identity, Persistence, and Background Queues.

## Interface
- `AiExtensions.AddInfrastructureAi`: Registers Gemini HTTP client (`IChatModelClient`), conversation store (`IAiConversationStore`), options binding, and `AiHistoryCleanupService` hosted service (`[AiExtensions.cs:AiExtensions]`)
- `IdentityExtensions.AddInfrastructureIdentity`: Configures ASP.NET Core Identity Core for `AppUser`, sets password policies, binds `JwtOptions`, and registers `ITokenService`, `IAuthService`, and `IUserAdminService` (`[IdentityExtensions.cs:IdentityExtensions]`)
- `PersistenceExtensions.AddInfrastructurePersistence`: Configures database context (`AppDbContext`) for SQLite or SQL Server, exposes `IAppDbContext`, registers singleton `ITenantProvider`, and registers database seeders (`[PersistenceExtensions.cs:PersistenceExtensions]`)
- `QueueExtensions.AddInfrastructureQueues`: Registers in-memory `RecalculationQueue` (`IRecalculationQueue`) as singleton and activates the `RecalculationWorker` hosted background service (`[QueueExtensions.cs:QueueExtensions]`)

## Invariants
- Startup configuration verification: `AiExtensions.AddInfrastructureAi` invokes immediate options validation upon service configuration (`[AiExtensions.cs:AddInfrastructureAi]`)
- Provider fallback: Defaults to SQLite connection string when `Database:Provider` is not explicitly set to SQL Server (`[PersistenceExtensions.cs:AddInfrastructurePersistence]`)
- Clean Architecture encapsulation: Resolves `IAppDbContext` abstraction from the DI container without exposing concrete EF Core assemblies to upper layers (`[PersistenceExtensions.cs:AddInfrastructurePersistence]`)

## Side Effects
- Modifies `IServiceCollection` DI container service descriptors and options configurations (`[AiExtensions.cs]`, `[IdentityExtensions.cs]`, `[PersistenceExtensions.cs]`, `[QueueExtensions.cs]`)

## Verification
- Run: `dotnet test backend/`

# PizzariaLucrativa.Infrastructure

## Overview
Infrastructure layer assembly implementing Clean Architecture external adapters, including Entity Framework Core relational persistence, ASP.NET Core Identity authentication with JWT token management, asynchronous background queues, and Google Gemini AI integration.

## Interface
- `InfrastructureServiceCollectionExtensions.AddInfrastructure`: Composition root aggregator registering persistence, identity, background recalculation queue, and AI services (`[DependencyInjection.cs:InfrastructureServiceCollectionExtensions]`)
- [Ai](./Ai/README.md): Google Gemini API client integration, conversation history store, and retention cleanup background service.
- [Extensions](./Extensions/README.md): Modular dependency injection extension methods decomposed by architectural concern.
- [Identity](./Identity/README.md): User authentication, JWT token issuance, refresh token rotation, ambient tenant context provider, and user administration.
- [Persistence](./Persistence/README.md): EF Core `AppDbContext`, multi-tenant global query filters, entity mappings, migrations, and database seeders.
- [Queues](./Queues/README.md): In-memory bounded queue and hosted service worker for asynchronous cascading recalculation.

## Invariants
- Clean Architecture dependency rule: The Infrastructure project references `Domain` and `Application`, implementing their interfaces without leaking EF Core or external dependencies into inner layers (`[PizzariaLucrativa.Infrastructure.csproj]`)
- Unified service bootstrapping: `AddInfrastructure` chains registrations in deterministic order: persistence -> identity -> queues -> AI (`[DependencyInjection.cs:AddInfrastructure]`)
- Tenant context guarantee: Ambient tenant state set by middleware or queue workers governs all tenant queries and entity insertions across the entire persistence lifecycle

## Side Effects
- Mutates and binds services, hosted background services, and configuration options into the `IServiceCollection` container (`[DependencyInjection.cs:AddInfrastructure]`)

## Verification
- Run: `dotnet test backend/`

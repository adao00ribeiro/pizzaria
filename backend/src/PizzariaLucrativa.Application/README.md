# PizzariaLucrativa.Application

## Overview
Application layer of PizzariaLucrativa following Clean Architecture principles. It encapsulates application use cases, orchestrates domain entities and calculators, declares repository and provider interfaces, exposes data transfer contracts, and integrates the Google Gemini AI assistant subsystem.

## Interface
- `ApplicationServiceCollectionExtensions`: Extension method `AddApplication(this IServiceCollection services)` registering Application services, in-memory cache, FluentValidation validators, and AI singletons/scoped dependencies into the dependency injection container.
- Subdirectories and contracts:
  - [Ai](./Ai/README.md): Gemini prompt builder, tool declarations, allowlist registry, and tool executor.
  - [DTOs](./DTOs/README.md): Data transfer objects, input payloads, answer blocks, and report models.
  - [Exceptions](./Exceptions/README.md): Standard application exceptions (`ValidationException`, `ConflictException`, `NotFoundException`, `UnauthorizedException`).
  - [Interfaces](./Interfaces/README.md): Contracts for persistence (`IAppDbContext`), ambient context (`ITenantProvider`, `ICurrentUser`), domain services, and external integrations.
  - [Options](./Options/README.md): Strongly-typed configuration options and validation rules for the AI subsystem (`AiOptions`).
  - [Serialization](./Serialization/README.md): Custom JSON converters for camelCase enum serialization (`LowerCaseEnumConverter`).
  - [Services](./Services/README.md): Use case implementations across catalog, pricing, orders, kitchen, stock, analytics, and AI assistant flows.
  - [Validators](./Validators/README.md): FluentValidation validators enforcing boundary and payload rules.

## Invariants
- `[ApplicationServiceCollectionExtensions:AddApplication]` Validates and registers all validators from the assembly containing `IngredientInputValidator`.
- `[PizzariaLucrativa.Application]` Never depends directly on persistence infrastructure (EF Core context classes) or web presentation layers (ASP.NET Core Controllers).
- `[PizzariaLucrativa.Application]` Respects tenant isolation boundaries enforced by `ITenantProvider` across all scoped business services.

## Side Effects
- Configures dependency injection registrations in `IServiceCollection`.
- Enforces scoped and singleton lifetimes for application services and in-memory caches.

## Verification
- Run: `dotnet test`

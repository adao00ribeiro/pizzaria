# Backend Source Projects (backend/src)

## Overview
Group of .NET 8 Clean Architecture projects that comprise the core PizzariaLucrativa backend solution.

## Subdirectories & Boundaries
- PizzariaLucrativa.Domain: [PizzariaLucrativa.Domain](./PizzariaLucrativa.Domain/README.md) - Core domain layer containing business entities, enumerations, domain exceptions, and calculation services.
- PizzariaLucrativa.Application: [PizzariaLucrativa.Application](./PizzariaLucrativa.Application/README.md) - Application use cases, service abstractions, DTOs, FluentValidation validators, and AI assistant tools.
- PizzariaLucrativa.Infrastructure: [PizzariaLucrativa.Infrastructure](./PizzariaLucrativa.Infrastructure/README.md) - Persistence via EF Core (AppDbContext), ASP.NET Identity, background recalculation queue, and external Google Gemini integration.
- PizzariaLucrativa.Api: [PizzariaLucrativa.Api](./PizzariaLucrativa.Api/README.md) - REST API presentation layer, controller endpoints, middleware, authentication policies, and Swagger documentation.

# PizzariaLucrativa.Api Package

## Overview
Presentation layer for the PizzariaLucrativa backend. Configures ASP.NET Core 8 Web API host, middleware pipeline, multi-tenant resolution, JSON serialization, security policies, and exposes REST endpoints.

## Interface
- `Program` [`Program.cs:Program`]: Application entry point and host bootstrapper; exposes `public partial class Program` for integration tests (`WebApplicationFactory`).
- [Controllers](./Controllers/README.md): REST endpoints for business operations, catalog, kitchen dispatch, AI assistant, and reports.
- [Extensions](./Extensions/README.md): Extension methods configuring dependency injection, CORS, rate limiting, and the HTTP request pipeline.
- [Filters](./Filters/README.md): Action execution filters executing FluentValidation validators.
- [Identity](./Identity/README.md): Current user claims extraction adapter.
- [Middleware](./Middleware/README.md): Global exception envelope mapping and tenant context resolution.

## Invariants
- `[Program.cs:Program]` Configures Serilog structured logging at startup before host initialization.
- `[Program.cs:Program]` Runs `DatabaseInitializer.InitializeDatabaseAsync` prior to starting HTTP request processing.
- `[Program.cs:Program]` Exposes `public partial class Program` to support integration test harnesses.

## Side Effects
- Binds to network ports and serves HTTP traffic.
- Executes database initialization and conditional seed data loading during startup.
- Writes structured logs to console and log sinks.

## Verification
- Run: `dotnet test`

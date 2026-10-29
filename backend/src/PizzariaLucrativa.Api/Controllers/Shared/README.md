# Controllers Shared Package

## Overview
Provides common base controller abstractions and conventions for all HTTP API endpoints in the PizzariaLucrativa presentation layer.

## Interface
- `ApiControllerBase` [`ApiControllerBase.cs:ApiControllerBase`]: Abstract base controller annotated with `[ApiController]`, `[ApiVersion("1.0")]`, and route prefix `api/v{version:apiVersion}/[controller]`.

## Invariants
- `[ApiControllerBase.cs:ApiControllerBase]` All REST API controllers must inherit from `ApiControllerBase` to ensure consistent API versioning and routing prefix `api/v{version:apiVersion}/[controller]`.
- `[ApiControllerBase.cs:ApiControllerBase]` Inherits from `ControllerBase` without view engine overhead.

## Side Effects
- None

## Verification
- Run: `dotnet test`

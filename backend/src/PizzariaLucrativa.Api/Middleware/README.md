# Middleware Package

## Overview
Houses HTTP middleware components responsible for global error handling, standard JSON error formatting, and multi-tenant context resolution.

## Interface
- `ExceptionMiddleware` [`ExceptionMiddleware.cs:ExceptionMiddleware`]: Intercepts unhandled application exceptions, mapping them to standard JSON envelopes (`{ status, message, errors }`).
- `TenantContextMiddleware` [`TenantContextMiddleware.cs:TenantContextMiddleware`]: Resolves the active tenant from the JWT `tenant_id` claim or public URL `{slug}` and sets it on `ITenantProvider`.

## Invariants
- `[ExceptionMiddleware.cs:ExceptionMiddleware]` Maps domain and application exceptions to appropriate HTTP status codes: 400 for validation / bad request, 401 for unauthorized, 404 for not found, 409 for conflict and `DbUpdateException` / concurrency issues, and 500 for unhandled errors.
- `[TenantContextMiddleware.cs:TenantContextMiddleware]` On `/api/v1/public/{slug}/...` routes, resolves tenant strictly by slug lookup against the database, throwing `NotFoundException` (404) if invalid.
- `[TenantContextMiddleware.cs:TenantContextMiddleware]` Always calls `tenantProvider.Clear()` inside a `finally` block to prevent tenant cross-contamination across requests.

## Side Effects
- Writes JSON error responses directly to the response body when exceptions occur.
- Queries the database for tenant lookup on public slug requests.
- Mutates scoped `ITenantProvider` state.

## Verification
- Run: `dotnet test`

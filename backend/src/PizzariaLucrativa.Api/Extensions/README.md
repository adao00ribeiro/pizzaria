# Extensions Package

## Overview
Provides modular `IServiceCollection` and `IApplicationBuilder` extension methods configuring ASP.NET Core dependency injection, middleware pipeline ordering, security, CORS, rate limiting, and API versioning.

## Interface
- `AuthenticationExtensions.AddApiJwtAuthentication` [`AuthenticationExtensions.cs:AddApiJwtAuthentication`]: Registers JWT Bearer authentication and token validation parameters.
- `AuthorizationExtensions.AddApiAuthorizationPolicies` [`AuthorizationExtensions.cs:AddApiAuthorizationPolicies`]: Registers the `OwnerOnly` authorization policy requiring the `Owner` role.
- `ControllersExtensions.AddApiControllers` [`ControllersExtensions.cs:AddApiControllers`]: Configures API controllers, registers `ValidationFilter`, suppresses default ModelState invalid filters, and configures JSON string enum converters.
- `CorsExtensions.AddApiCors` [`CorsExtensions.cs:AddApiCors`]: Configures CORS policy "DefaultCors" with allowed origins from configuration or localhost defaults.
- `CurrentUserExtensions.AddApiCurrentUser` [`CurrentUserExtensions.cs:AddApiCurrentUser`]: Registers HTTP context access and scopes `ICurrentUser` to `HttpContextCurrentUser`.
- `PipelineExtensions.UseApiPipeline` [`PipelineExtensions.cs:UseApiPipeline`]: Assembles the middleware pipeline in order: Exception, StatusCodePages, CORS, RateLimiter, Swagger, Authentication, TenantContext, Authorization, and Controllers.
- `RateLimitingExtensions.AddApiRateLimiting` [`RateLimitingExtensions.cs:AddApiRateLimiting`]: Configures client IP fixed-window rate limiting policies returning HTTP 429.
- `StatusCodePagesExtensions.UseApiStatusCodePages` [`StatusCodePagesExtensions.cs:UseApiStatusCodePages`]: Translates empty 401, 403, and 404 HTTP responses into standard JSON error envelopes.
- `SwaggerExtensions.AddApiSwagger` [`SwaggerExtensions.cs:AddApiSwagger`]: Configures Swagger document generation and Bearer security definitions.
- `SwaggerExtensions.UseApiSwagger` [`SwaggerExtensions.cs:UseApiSwagger`]: Enables Swagger middleware and Swagger UI endpoints.
- `VersioningExtensions.AddApiVersioningConfiguration` [`VersioningExtensions.cs:AddApiVersioningConfiguration`]: Configures URL-segment API versioning defaulting to version `1.0`.

## Invariants
- `[PipelineExtensions.cs:UseApiPipeline]` Middleware order is fixed: `ExceptionMiddleware` -> `StatusCodePages` -> `CORS` -> `RateLimiter` -> `Swagger` -> `Authentication` -> `TenantContextMiddleware` -> `Authorization` -> `Controllers`.
- `[ControllersExtensions.cs:AddApiControllers]` `SuppressModelStateInvalidFilter` must remain `true` to ensure model errors pass through `ValidationFilter` to produce standard error envelopes.
- `[AuthorizationExtensions.cs:AddApiAuthorizationPolicies]` `OwnerOnly` policy strictly checks `ClaimTypes.Role == "Owner"`.

## Side Effects
- Mutates `IServiceCollection` service registrations.
- Modifies HTTP request pipeline execution chain.

## Verification
- Run: `dotnet test`

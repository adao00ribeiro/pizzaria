# Identity Infrastructure

## Overview
Identity and security infrastructure implementing multi-tenant user authentication, JWT access token generation, cryptographically secure refresh token rotation, ambient tenant context propagation, and user administration.

## Interface
- `JwtOptions`: Configuration model for JWT key, issuer, audience, and token lifetime (`[TokenService.cs:JwtOptions]`)
- `ITokenService` / `TokenService`: Generates HS256-signed JWT access tokens with user ID, email, tenant, and role claims (`[TokenService.cs:TokenService]`)
- `TenantProvider`: Implements `ITenantProvider` to store and expose the active tenant identifier across asynchronous execution contexts via `AsyncLocal<Guid?>` (`[TenantProvider.cs:TenantProvider]`)
- `AuthService`: Implements `IAuthService` for tenant registration, password credential authentication, and single-use refresh token rotation (`[AuthService.cs:AuthService]`)
- `UserAdminService`: Implements `IUserAdminService` for tenant-scoped user management, role modification, and last-owner protection (`[UserAdminService.cs:UserAdminService]`)

## Invariants
- Secret key strength: `JwtOptions.Key` must contain at least 32 characters (`[TokenService.cs:TokenService]`)
- Ambient tenant resolution: `TenantProvider.GetTenantId()` throws `UnauthorizedException` if no tenant context is established (`[TenantProvider.cs:GetTenantId]`)
- Refresh token rotation: Used refresh tokens are immediately revoked (`RevokedAt = DateTime.UtcNow`) upon renewal and tokens are stored solely as SHA-256 hashes (`[AuthService.cs:RefreshAsync]`)
- Last owner guard: Prevents demoting or deleting the final user holding the `Owner` role in a tenant (`[UserAdminService.cs:IsLastOwnerAsync]`)
- Tenant boundary enforcement: `UserAdminService` filters all user queries by the authenticated caller's tenant ID, returning 404 for non-existent or foreign users (`[UserAdminService.cs:FindAsync]`)

## Side Effects
- Database transactions persisting records to `Tenants`, `AspNetUsers`, `AspNetUserRoles`, `FeeConfigs`, and `RefreshTokens` (`[AuthService.cs]`, `[UserAdminService.cs]`)
- Mutates thread-local/async context state via `AsyncLocal<Guid?>` (`[TenantProvider.cs:SetTenant]`)

## Verification
- Run: `dotnet test backend/`

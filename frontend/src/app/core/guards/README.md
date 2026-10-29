# Core Route Guards

## Overview
Angular functional route guards that enforce access control across application routes based on JWT authentication, role-based authorization policies, and tenant initialization state.

## Interface
- `auth.guard.ts:authGuard`: CanActivateFn ensuring user is authenticated or holds an active refresh token before accessing protected routes.
- `role.guard.ts:roleGuard`: Factory returning a CanActivateFn restricting route access to specified roles ("owner", "atendente", "pizzaiolo", etc.).
- `tenant.guard.ts:tenantGuard`: CanActivateFn ensuring tenant pizzeria configuration is preloaded into application state before entering the shell.

## Invariants
- `[auth.guard.ts:authGuard]`: Unauthenticated navigations lacking refresh tokens are redirected to `/login`.
- `[role.guard.ts:roleGuard]`: When `AuthService.role()` is null (legacy token without claim), navigation is allowed through to let the backend enforce authorization.
- `[tenant.guard.ts:tenantGuard]`: Network failure during tenant preloading does not block route resolution; navigation proceeds with cached or fallback tenant data.

## Side Effects
- `[tenant.guard.ts:tenantGuard]`: Triggers HTTP GET `/tenant` via `TenantService.ensureLoaded()` when tenant data is not yet cached.
- `[auth.guard.ts:authGuard, role.guard.ts:roleGuard]`: Alters Angular Router navigation URL tree upon access rejection.

## Verification
- Run: `npm test --prefix frontend`

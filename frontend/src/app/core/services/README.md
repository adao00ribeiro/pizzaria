# Core Application Services

## Overview
Singleton application services providing state management, authentication flows, browser storage persistence, Progressive Web App (PWA) update lifecycle, and light/dark theme synchronization using Angular Signals.

## Interface
- `auth.service.ts:AuthService`: Manages user login, registration, JWT token state, claims parsing, role queries, and logout flow.
- `pwa.service.ts:PwaService`: Tracks online/offline connectivity, manages PWA installation prompt events, and coordinates Service Worker update installations.
- `storage.service.ts:StorageService`: Safely wraps `localStorage` access for session tokens and tenant identifiers with graceful fallback.
- `tenant.service.ts:TenantService`: Loads, caches, and updates current tenant pizzeria metadata (name and public slug).
- `theme.service.ts:ThemeService`: Manages application theme mode (`light` vs `dark`), DOM root attributes, and persistence in local storage.

## Invariants
- `[auth.service.ts:AuthService]`: Concurrent 401 refresh requests share a single in-flight HTTP POST `/auth/refresh` observable via `shareReplay`.
- `[storage.service.ts:StorageService]`: Storage exceptions (e.g. disabled cookies/incognito quotas) are caught silently, retaining volatile state in memory.
- `[theme.service.ts:ThemeService]`: Initializes default theme from `localStorage` if present, falling back to OS `prefers-color-scheme`.

## Side Effects
- `[auth.service.ts:AuthService, tenant.service.ts:TenantService]`: Performs HTTP communication with `/auth/*` and `/tenant` endpoints.
- `[storage.service.ts:StorageService, theme.service.ts:ThemeService]`: Reads and writes browser `localStorage`.
- `[theme.service.ts:ThemeService]`: Modifies `document.documentElement` attributes and `document.body` CSS class list.
- `[pwa.service.ts:PwaService]`: Registers event listeners on window (`online`, `offline`, `beforeinstallprompt`).

## Verification
- Run: `npm test --prefix frontend`

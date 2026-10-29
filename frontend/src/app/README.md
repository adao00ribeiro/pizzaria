# Application Root Module

## Overview
Root application component and dependency injection provider configuration wiring global interceptors, Portuguese localization (`pt-BR`), ng2-charts, service worker, and routing hierarchies.

## Interface
- `app.component.ts:AppComponent`: Root Angular component hosting `<router-outlet>`.
- `app.config.ts:appConfig`: ApplicationConfig object configuring global providers, HTTP interceptors (`jwtInterceptor`, `errorInterceptor`), animations, and PWA service worker.
- `app.routes.ts:routes`: Complete application route table organizing public storefront, authentication, and shell feature paths guarded by `authGuard`, `roleGuard`, and `tenantGuard`.
- [Core Module](./core/README.md): Core services, models, guards, interceptors, and utilities.
- [Features Module](./features/README.md): Business domain feature modules.
- [Layout Module](./layout/README.md): Shell layout container and standalone authentication pages.
- [Shared Module](./shared/README.md): Reusable UI components and pipes.

## Invariants
- `[app.config.ts:appConfig]`: Registers `pt-BR` as the application-wide `LOCALE_ID`.
- `[app.config.ts:appConfig]`: Enables Angular Service Worker (`ngsw-worker.js`) only in non-dev mode.

## Side Effects
- `[app.config.ts:appConfig]`: Registers Portuguese locale data into Angular common runtime.

## Verification
- Run: `npm test --prefix frontend`

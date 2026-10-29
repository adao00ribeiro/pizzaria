# Frontend Source Blueprint

## System Overview
Angular 18 standalone single-page application and Progressive Web App (PWA) client for Pizzaria Lucrativa (Posachio), providing manager dashboards, POS order processing, kitchen display queues, technical sheet costing, AI assistance, and public storefronts.

## Local Interface & Invariants
- `main.ts`: Application bootstrap entry point initializing `AppComponent` with `appConfig` and Portuguese (`pt-BR`) locale.
- `index.html`: Shell HTML document with Material font links, viewport meta tags, and root `<pz-root>` selector.
- `styles.scss`: Global Sass styling definitions, CSS custom properties for theming, and Angular Material component overrides.

## Subdirectories & System Boundaries
- Application Root: [./app/README.md](./app/README.md) - Application routes, dependency injection configuration, and feature/core/layout subtrees.
- Environment Profiles: [./environments/README.md](./environments/README.md) - Build environment targets for local dev and production reverse proxies.

## Global Invariants
- All authenticated API calls attach JWT Bearer tokens and automatically attempt token refresh once on HTTP 401.
- All monetary and ratio presentations adhere to Brazilian Portuguese locale standards (`pt-BR`, `R$`, `,`).
- Multi-tenant isolation is preserved via route guards ensuring tenant data is resolved and attached to user session state.

## Global Side Effects
- Mutates browser `localStorage` for authentication tokens, theme preferences, and customer shopping carts.
- Initiates HTTP requests to backend REST API endpoints (`/api/v1/*`) and external address lookups via ViaCEP.
- Synthesizes audio cues via Web Audio API and manages service worker lifecycle.

## System Verification
- Run: `npm test --prefix frontend`

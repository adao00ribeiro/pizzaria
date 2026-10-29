# Public Ordering and Tracking Feature

## Overview
Unauthenticated customer-facing digital menu, shopping cart checkout, and real-time order status tracking accessed via tenant public slug.

## Interface
- `public-menu.page.ts:PublicMenuPage`: Digital menu displaying pizza flavors, sizes, drinks, and combos for customer selection.
- `public-menu.service.ts:PublicMenuService`: Service fetching public tenant menus (`/public/{slug}/menu`), submitting orders (`/public/{slug}/orders`), looking up tracking (`/public/{slug}/tracking/{id}`), and querying ViaCEP (`viacep.com.br`).
- `public-checkout.page.ts:PublicCheckoutPage`: Checkout flow gathering delivery address, CEP autofill, payment choice, and order placement.
- `public-tracking.page.ts:PublicTrackingPage`: Timeline progress tracker for placed customer orders.

## Invariants
- `[public-menu.service.ts:PublicMenuService]`: Public routes execute without requiring JWT authentication tokens.
- `[public-checkout.page.ts:PublicCheckoutPage]`: Queries ViaCEP to automatically resolve street, neighborhood, and city upon entering an 8-digit postal code.

## Side Effects
- `[public-menu.service.ts:PublicMenuService]`: Dispatches HTTP calls to `/public/*` and external GET requests to `viacep.com.br/ws/{cep}/json/`.
- `[public-menu.service.ts:PublicMenuService]`: Stores customer cart draft in `localStorage`.

## Verification
- Run: `npm test --prefix frontend`

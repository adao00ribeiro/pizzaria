# Not Found Page

## Overview
Fallback 404 error page displayed when users navigate to undefined application routes.

## Interface
- `not-found.component.ts:NotFoundComponent`: Standalone presentation component displaying an error notice and link back to dashboard.

## Invariants
- `[not-found.component.ts:NotFoundComponent]`: Always provides a direct navigational path back to `/dashboard`.

## Side Effects
- None

## Verification
- Run: `npm test --prefix frontend`

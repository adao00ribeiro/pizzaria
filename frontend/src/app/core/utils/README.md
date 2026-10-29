# Core Utilities

## Overview
Pure utility functions for standardizing data structures across heterogeneous backend API responses.

## Interface
- `page.util.ts:normalizePage`: Normalizes either a raw array or a paginated envelope (`PagedResult<T>`) into a consistent `{ items: T[], total: number }` structure.
- `page.util.ts:normalizeItems`: Extracts the item array from either a raw array or a `PagedResult<T>` envelope.

## Invariants
- `[page.util.ts:normalizePage]`: Always returns an object containing an `items` array and a numeric `total` count, defaulting to empty arrays and zero on nullish input.

## Side Effects
- None

## Verification
- Run: `npm test --prefix frontend`

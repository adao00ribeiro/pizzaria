# Operational Fees Feature

## Overview
Configuration interface for transaction fees, payment gateway cuts, delivery app commissions, and public tenant slug customization.

## Interface
- `fees.page.ts:FeesPage`: Form page allowing owners to adjust payment fees, delivery percentage splits, and public store handles.
- `fees.service.ts:FeesService`: HTTP client service retrieving and updating fee structures via `/costs/fees`.

## Invariants
- `[fees.page.ts:FeesPage]`: Validates that percentage and monetary fee rates do not exceed defined domain thresholds.

## Side Effects
- `[fees.service.ts:FeesService]`: Dispatches HTTP GET and PUT requests to `/costs/fees`.

## Verification
- Run: `npm test --prefix frontend`

# Stock Movements and Replenishment Feature

## Overview
Inventory audit ledger tracking stock transactions (inbound purchases, preparation deductions, order usage, manual adjustments) and AI-driven restocking urgency reports.

## Interface
- `stock.page.ts:StockPage`: Inventory dashboard displaying movement audit logs and suggested replenishment buy orders.
- `stock.service.ts:StockService`: HTTP client handling stock logs and replenishment recommendations via `/stock/movements` and `/stock/replenishment-report`.

## Invariants
- `[stock.service.ts:StockService]`: Replenishment suggestions classify inventory urgency into Critical, Alert, Normal, or Surplus based on run-out estimates.

## Side Effects
- `[stock.service.ts:StockService]`: Dispatches HTTP calls to `/stock/*`.

## Verification
- Run: `npm test --prefix frontend`

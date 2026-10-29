# Delivery Neighborhoods Feature

## Overview
Management of delivery zones, delivery fee schedules, estimated delivery minutes, and active delivery neighborhood coverage.

## Interface
- `neighborhoods.page.ts:NeighborhoodsPage`: Table view listing delivery zones, fee amounts, and coverage toggles.
- `neighborhoods.service.ts:NeighborhoodsService`: HTTP client handling CRUD operations for delivery zones via `/neighborhoods`.
- `neighborhood-form.dialog.ts:NeighborhoodFormDialog`: Dialog modal for adding or editing delivery neighborhoods and fees.
- `neighborhood-form.dialog.ts:NeighborhoodDialogData`: Input data interface for the neighborhood edit dialog.

## Invariants
- `[neighborhoods.service.ts:NeighborhoodsService]`: Delivery fees and estimated delivery minutes cannot be negative numbers.

## Side Effects
- `[neighborhoods.service.ts:NeighborhoodsService]`: Dispatches HTTP GET, POST, PUT, and DELETE calls to `/neighborhoods`.

## Verification
- Run: `npm test --prefix frontend`

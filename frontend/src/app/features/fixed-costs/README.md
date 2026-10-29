# Fixed Costs Feature

## Overview
Management module for monthly recurring operational overheads (rent, salaries, utilities, software subscriptions) incorporated into unit margin formulas.

## Interface
- `fixed-costs.page.ts:FixedCostsPage`: Data table listing recurring fixed costs with category filters and sum total calculations.
- `fixed-costs.service.ts:FixedCostsService`: HTTP service managing CRUD operations against `/costs/fixed`.
- `fixed-cost-form.dialog.ts:FixedCostFormDialog`: Modal dialog for creating and updating individual fixed cost entries.
- `fixed-cost-form.dialog.ts:FixedCostDialogData`: Injection data contract for the cost form dialog.

## Invariants
- `[fixed-costs.service.ts:FixedCostsService]`: Monthly cost amounts must be positive decimal numbers.

## Side Effects
- `[fixed-costs.service.ts:FixedCostsService]`: Dispatches HTTP GET, POST, PUT, and DELETE calls to `/costs/fixed`.

## Verification
- Run: `npm test --prefix frontend`

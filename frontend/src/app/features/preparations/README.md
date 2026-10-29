# Preparations Feature

## Overview
Management of intermediate recipes and semi-finished preparations (doughs, special sauces, pre-cooked proteins) composed of raw ingredients with calculated unit yields.

## Interface
- `preparations.page.ts:PreparationsPage`: List of registered semi-finished preparations showing yield units and total batch costs.
- `preparations.service.ts:PreparationsService`: HTTP service handling CRUD operations and cost breakdowns via `/preparations`.
- `preparation-editor.dialog.ts:PreparationEditorDialog`: Dialog modal for assembling preparation ingredient lines and batch yields.
- `preparation-editor.dialog.ts:RecipeOption`: Selection option interface for ingredient items.

## Invariants
- `[preparations.service.ts:PreparationsService]`: Batch yields must be strictly greater than zero to calculate valid unit costs.

## Side Effects
- `[preparations.service.ts:PreparationsService]`: Dispatches HTTP calls to `/preparations`.

## Verification
- Run: `npm test --prefix frontend`

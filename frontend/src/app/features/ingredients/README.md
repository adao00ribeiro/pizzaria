# Ingredients Feature

## Overview
Raw material inventory management, ingredient unit cost tracking, usage reference inspection, and direct stock replenishment.

## Interface
- `ingredients.page.ts:IngredientsPage`: Table view of raw ingredients with current stock levels, unit costs, and filter chips.
- `ingredients.service.ts:IngredientsService`: HTTP client handling CRUD operations and stock replenishment via `/ingredients`.
- `ingredient-form.dialog.ts:IngredientFormDialog`: Dialog modal for registering or updating ingredient definitions.
- `ingredient-form.dialog.ts:IngredientUsagesDialog`: Dialog displaying technical sheets where an ingredient is currently referenced.
- `replenish-stock.dialog.ts:ReplenishStockDialog`: Quick modal for logging stock entry and unit purchase prices.

## Invariants
- `[ingredients.service.ts:IngredientsService]`: Ingredients linked to existing technical sheets cannot be hard-deleted without confirmation or unlinking.

## Side Effects
- `[ingredients.service.ts:IngredientsService]`: Dispatches HTTP calls to `/ingredients` and `/ingredients/{id}/usages`.

## Verification
- Run: `npm test --prefix frontend`

# Pricing Intelligence Feature

## Overview
Pricing calculators and margin simulators covering multi-flavor split pizzas, promotional combo bundles, and retail beverage markups.

## Interface
- `pricing.service.ts:PricingService`: HTTP service calling `/pricing/calculate-mixed` and `/pricing/calculate-product` for real-time margin simulations.
- `half-half.page.ts:HalfHalfPage`: Interactive simulator for calculating 2, 3, or 4 flavor split pizza pricing rules (average vs highest).
- `combos.page.ts:CombosPage`: List and profitability analysis of combo bundles.
- `combos.service.ts:CombosService`: HTTP service managing combo bundles and margin previews via `/pricing/combos`.
- `combo-form.dialog.ts:ComboFormDialog`: Dialog modal for assembling combo products and simulating bundle profitability.
- `beverages.page.ts:BeveragesPage`: Beverage catalog margin table with markup adjustments.
- `beverages.service.ts:BeveragesService`: HTTP service managing beverage pricing and markup targets via `/products/beverages`.

## Invariants
- `[pricing.service.ts:PricingService]`: Requests silent HTTP error contexts to prevent popup snackbars during live keystroke calculations.

## Side Effects
- `[pricing.service.ts:PricingService, combos.service.ts:CombosService, beverages.service.ts:BeveragesService]`: Dispatches HTTP calls to `/pricing/*` and `/products/beverages`.

## Verification
- Run: `npm test --prefix frontend`

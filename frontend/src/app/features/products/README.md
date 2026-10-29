# Products Catalog Feature

## Overview
Product menu catalog management supporting multi-category items (pizzas, calzones, sides) and pizza size variations (broto, média, grande, família).

## Interface
- `products.page.ts:ProductsPage`: Tabbed catalog view for products and pizza size configurations.
- `products.service.ts:ProductsService`: HTTP client service performing CRUD operations for products and sizes via `/products` and `/products/sizes`.
- `product-form.dialog.ts:ProductFormDialog`: Dialog modal for registering and editing menu products.
- `size-form.dialog.ts:SizeFormDialog`: Dialog modal for managing pizza sizes, slice counts, and maximum allowed split flavors.

## Invariants
- `[products.service.ts:ProductsService]`: Pizza sizes enforce positive slice counts and flavor splits between 1 and 4.

## Side Effects
- `[products.service.ts:ProductsService]`: Dispatches HTTP calls to `/products` and `/products/sizes`.

## Verification
- Run: `npm test --prefix frontend`

# PizzariaLucrativa.Application.Validators

## Overview
Contains FluentValidation validator classes for input transfer objects across the catalog, pricing, stock, configuration, and conversational AI endpoints. Ensures boundary invariants, structural constraints, and numeric ranges prior to domain service execution.

## Interface
- `ChatRequestDtoValidator`: Validates AI message payload length and non-emptiness (`Message` max 2,000 characters).
- `ComboInputValidator`: Enforces combo name length, item presence, positive quantities, and non-negative discounts.
- `FeeConfigInputValidator`: Validates percentage ranges for card fee, delivery fee, default profit margin (0% to 100% exclusive), and global loss percentage.
- `FixedCostInputValidator`: Validates monthly cost amounts and verifies allocation-specific fields (`ExpectedPizzasPerMonth` or `ExpectedMonthlyRevenue`).
- `HalfHalfInputValidator`: Enforces that half-and-half pizza requests contain either 2 or 4 flavor selections.
- `IngredientInputValidator`: Validates positive purchase prices, quantities, and loss percentage boundaries for raw ingredients.
- `PreparationInputValidator`: Enforces sub-recipe name, positive yield quantity, and valid ingredient or sub-preparation item linkages.
- `ProductInputValidator`: Validates product names, image URL lengths, and optional pricing/stock parameters.
- `SizeInputValidator`: Validates pizza size names and positive diameter values.
- `StockMovementInputValidator`: Validates ingredient movements, requiring non-zero quantities and mandatory unit costs for incoming stock.
- `TechnicalSheetInputValidator`: Validates technical sheet packaging costs, item references, and margin overrides.

## Invariants
- `[Validators:*]` All validators inherit from FluentValidation's `AbstractValidator<T>`.
- `[HalfHalfInputValidator]` Flavor ID lists must contain exactly 2 or 4 items.
- `[StockMovementInputValidator]` When `Type == StockMovementType.In`, `UnitCost` must be explicitly specified and non-negative.
- `[FeeConfigInputValidator]` `DefaultMarginPercentage` must fall strictly within the exclusive range `(0, 100)`.

## Side Effects
- None (pure in-memory validation rules).

## Verification
- Run: `dotnet test`

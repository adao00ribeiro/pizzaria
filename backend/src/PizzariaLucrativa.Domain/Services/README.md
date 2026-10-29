# PizzariaLucrativa.Domain.Services

## Overview
Pure computational domain services, financial and pricing calculators, inventory usage aggregators, code generators, and unit converters. Operates purely on immutable data models with zero external I/O or database dependencies.

## Interface
- `ICalculationSource`: Snapshot contract for querying ingredient and sub-recipe data during pricing execution (`[CalculationModels.cs:ICalculationSource]`)
- Calculation Models (`CalculationModels.cs`): Immutable records (`IngredientData`, `PreparationItemData`, `PreparationData`, `SheetItemData`, `FixedCostInput`, `FixedCostAllocation`, `SheetPricingInput`, `BreakdownItem`, `PricingResult`, `PreparationCostResult`, `ComboItemInput`, `ComboPricingInput`, `ComboResult`, `MistaFlavor`, `MistaResult`) representing pricing inputs, cost allocations, recipe items, and calculation results.
- `OrderProfitEntry`: Single order financial metrics record for profit auditing (`[ProfitReportCalculator.cs:OrderProfitEntry]`)
- `ProfitPeriodSummary`: Consolidated financial totals across a defined operational period (`[ProfitReportCalculator.cs:ProfitPeriodSummary]`)
- `StockReplenishmentItemInput`: Input parameters for ingredient replenishment calculations (`[StockReplenishmentCalculator.cs:StockReplenishmentItemInput]`)
- `StockReplenishmentItemResult`: Replenishment recommendation and urgency rating for an individual ingredient (`[StockReplenishmentCalculator.cs:StockReplenishmentItemResult]`)
- `StockReplenishmentSummaryResult`: Aggregated replenishment expenditure and item summaries (`[StockReplenishmentCalculator.cs:StockReplenishmentSummaryResult]`)
- `OrderCodeGenerator`: Static generator and validator for 6-character customer tracking codes (`[OrderCodeGenerator.cs:OrderCodeGenerator]`)
- `PricingCalculator`: Pure pricing engine for technical sheets, multi-flavor pizzas, and combos (`[PricingCalculator.cs:PricingCalculator]`)
- `ProfitReportCalculator`: Consolidated revenue, cost, fee, and margin reporting calculator (`[ProfitReportCalculator.cs:ProfitReportCalculator]`)
- `SlugGenerator`: URL-friendly tenant slug generation, formatting, and validation (`[SlugGenerator.cs:SlugGenerator]`)
- `StockReplenishmentCalculator`: Inventory reorder calculator evaluating urgency levels and suggested purchases (`[StockReplenishmentCalculator.cs:StockReplenishmentCalculator]`)
- `StockUsageCalculator`: Recursive recipe decomposition engine computing raw ingredient draw (`[StockUsageCalculator.cs:StockUsageCalculator]`)
- `UnitConverter`: Metric weight and volume conversion utility (`[UnitConverter.cs:UnitConverter]`)

## Invariants
- Functional purity (`NF-R01`): All calculators operate exclusively on immutable input snapshots and return deterministic output with zero database access or network I/O.
- Cycle detection: `[PricingCalculator.cs:PricingCalculator.PreparationCost]` and `[StockUsageCalculator.cs:StockUsageCalculator.AccumulatePreparation]` trace visited preparation identifiers and throw `[Exceptions/CycleDetectedException]` if circular references exist.
- Metric unit consistency: `[UnitConverter.cs:UnitConverter.Factor]` restricts conversions to compatible physical dimensions (`g` <-> `kg`, `ml` <-> `L`) and throws `DomainException` upon incompatible conversion attempts.
- Tracking code syntax: `[OrderCodeGenerator.cs:OrderCodeGenerator.Generate]` yields cryptographically random 6-character strings using a 32-character unambiguous alphabet (`ABCDEFGHJKLMNPQRSTUVWXYZ23456789`).
- Tenant slug syntax: `[SlugGenerator.cs:SlugGenerator.IsValid]` restricts valid slugs to lowercase alphanumeric characters separated by single hyphens between 3 and 40 characters in length.

## Side Effects
- None

## Verification
- Run: `dotnet test`

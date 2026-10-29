# PizzariaLucrativa.Application.Services

## Overview
Implements application orchestration services and business use cases across catalog management, pricing calculations, order workflows, kitchen production queues, inventory management, operational reports, and AI assistant dialogues. Coordinates domain models, pure calculators, database persistence, and external service clients.

## Interface
- AI Assistant Services:
  - `AiChatService`: Manages multi-turn conversations, tool calling loops, backend chart construction, and prompt-injection defense.
  - `AiUsageService`: Records token consumption and enforces monthly spending caps per tenant.
- Catalog & Pricing Services:
  - `IngredientService`: Manages ingredients, unit costs, and dependency checks.
  - `PreparationService`: Manages sub-recipes, yield calculations, and cycle detection.
  - `ProductService`, `SizeService`, `BeverageService`: Manages catalog entities and beverage markup pricing.
  - `TechnicalSheetService`: Manages technical sheets and item cost snapshots.
  - `PricingService`: Orchestrates pricing calculations, margin analysis, and half-and-half configurations.
  - `ComboService`: Orchestrates combo pricing and discount margins.
  - `FixedCostService`, `FeeService`: Manages operating overheads and payment/delivery fee configurations.
- Operations, Orders & Stock:
  - `OrderService`: Handles order creation, status transitions, thermal ticket printing, and public order tracking.
  - `KitchenService`: Serves the kitchen kanban queue and handles cooking status updates.
  - `StockService`: Tracks ingredient inventory levels and logs manual/automatic movements.
  - `StockConsumptionService`: Idempotently deducts ingredients for completed orders and reverses stock on cancellation.
  - `StockReplenishmentService`: Generates procurement recommendations and stock deficit reports.
  - `NeighborhoodService`: Manages delivery regions and neighborhood definitions.
- Analytics, Reports & Alerts:
  - `AnalyticsService`: Aggregates order volumes, heatmaps, and regional metrics.
  - `ReportService`: Consolidates real profit, cost of goods sold (CMV), and sales channels.
  - `ReportPeriodResolver`, `DateTimeProvider`: Provides deterministic date ranges and current UTC time.
  - `DashboardService`: Compiles high-level business KPIs and margin summaries.
  - `AlertService`: Tracks and resolves low-margin and low-stock alerts.
  - `RecalculationService`: Re-evaluates costs across all recipes upon ingredient or fee modifications.
  - `TenantService`: Manages tenant configuration and profile metadata.

## Invariants
- `[IngredientService:DeleteAsync]` Throws `ConflictException` if the ingredient is referenced in active technical sheets or preparations.
- `[PreparationService:ValidateNoCycle]` Prevents circular references in sub-recipes.
- `[OrderService:CreateAsync]` Total order costs, items, and delivery fees are recomputed server-side from active catalog entities.
- `[StockConsumptionService:ConsumeForOrderAsync]` Stock deduction is strictly idempotent; an order cannot deduct inventory more than once.
- `[AiChatService:AskAsync]` LLM interactions are aborted if the tenant's monthly budget is exceeded, and tool loop iterations are bounded by `MaxToolRounds`.

## Side Effects
- Mutates database state across entities via `IAppDbContext.SaveChangesAsync()`.
- Dispatches background recalculation tasks via `IRecalculationQueue`.
- Makes outbound HTTPS requests to Google Gemini via `IChatModelClient`.
- Emits log records via `ILogger`.

## Verification
- Run: `dotnet test`

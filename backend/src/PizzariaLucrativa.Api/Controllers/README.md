# Controllers Package

## Overview
Exposes RESTful HTTP endpoints for all business modules in PizzariaLucrativa, including authentication, pizza recipes and technical sheets, pricing calculators, stock and inventory management, kitchen dispatch, analytics, AI assistant chat, and multi-tenant management.

## Interface
- [Shared](./Shared/README.md): Common base controller contracts and API version routing defaults.
- `AiController` [`AiController.cs:AiController`]: Endpoints for Gemini AI assistant chat, suggestions, conversations, monthly token usage, and feedback (`[Authorize(Policy = "OwnerOnly")]`).
- `AlertsController` [`AlertsController.cs:AlertsController`]: Endpoints to list unread operational alerts, get count, and resolve alert notifications (`[Authorize]`).
- `AnalyticsController` [`AnalyticsController.cs:AnalyticsController`]: Endpoints for regional sales reports, order summary metrics, heatmaps, and period comparisons (`[Authorize(Policy = "OwnerOnly")]`).
- `AuthController` [`AuthController.cs:AuthController`]: Public authentication endpoints for user registration, login, and JWT token refresh (`[AllowAnonymous]`).
- `BeveragesController` [`BeveragesController.cs:BeveragesController`]: Endpoints for managing beverages and calculating markups (`[Authorize]`).
- `CombosController` [`CombosController.cs:CombosController`]: Endpoints for combo promotion CRUD and combo margin calculation (`[Authorize]`).
- `DashboardController` [`DashboardController.cs:DashboardController`]: Executive dashboard metrics, product rankings, and margin distributions (`[Authorize(Policy = "OwnerOnly")]`).
- `FeesController` [`FeesController.cs:FeesController`]: Payment card and delivery platform fee configuration (`[Authorize]`).
- `FixedCostsController` [`FixedCostsController.cs:FixedCostsController`]: Monthly fixed cost management endpoints (`[Authorize]`).
- `IngredientsController` [`IngredientsController.cs:IngredientsController`]: Ingredient catalog, purchasing unit costs, and yield factors (`[Authorize]`).
- `KitchenController` [`KitchenController.cs:KitchenController`]: Kitchen display system (KDS) active order queries and status transitions (`[Authorize]`).
- `NeighborhoodsController` [`NeighborhoodsController.cs:NeighborhoodsController`]: Delivery neighborhood boundaries and fee matrix (`[Authorize]`).
- `OrdersController` [`OrdersController.cs:OrdersController`]: Order placement, pricing simulations, listing, and lifecycle updates (`[Authorize]`).
- `PreparationsController` [`PreparationsController.cs:PreparationsController`]: Pre-prepared base ingredients (dough, sauces) and batch recipes (`[Authorize]`).
- `PricingController` [`PricingController.cs:PricingController`]: Pricing simulations and batch price recalculation endpoints (`[Authorize]`).
- `ProductsController` [`ProductsController.cs:ProductsController`]: Pizza and menu item catalog management (`[Authorize]`).
- `PublicController` [`PublicController.cs:PublicController`]: Public customer menu browsing and customer order tracking by slug (`[AllowAnonymous]`).
- `ReportsController` [`ReportsController.cs:ReportsController`]: Detailed profit reports by day, product, and sales channel (`[Authorize(Policy = "OwnerOnly")]`).
- `SizesController` [`SizesController.cs:SizesController`]: Pizza size dimensions and slice capacity configuration (`[Authorize]`).
- `StockController` [`StockController.cs:StockController`]: Current inventory balances, manual movement entries, and replenishment advice (`[Authorize]`).
- `TechnicalSheetsController` [`TechnicalSheetsController.cs:TechnicalSheetsController`]: Technical sheet composition, CMV calculation, and margin analysis (`[Authorize]`).
- `TenantController` [`TenantController.cs:TenantController`]: Tenant profile configuration and pizzeria settings (`[Authorize]`).
- `UsersController` [`UsersController.cs:UsersController`]: Employee user management, permissions, and role assignment (`[Authorize(Policy = "OwnerOnly")]`).

## Invariants
- `[ApiControllerBase.cs:ApiControllerBase]` All controllers inherit from `ApiControllerBase` and follow `api/v{version:apiVersion}/[controller]` routing.
- `[AuthController.cs, PublicController.cs]` Only `AuthController` and `PublicController` allow anonymous access via `[AllowAnonymous]`; all other controllers require authentication (`[Authorize]`).
- `[AiController.cs, AnalyticsController.cs, DashboardController.cs, ReportsController.cs, UsersController.cs]` Restrict access strictly to users possessing the `Owner` role via `[Authorize(Policy = "OwnerOnly")]`.
- `[PublicController.cs:PublicController]` Tenant context is resolved exclusively from the URL `{slug}` parameter.

## Side Effects
- Mutates database state via Application services upon `POST`, `PUT`, `DELETE` requests.
- Invokes external Google Gemini AI endpoints for chat completions in `AiController`.
- Writes JSON error responses upon validation or unhandled exceptions.

## Verification
- Run: `dotnet test`

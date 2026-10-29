# Core Domain Models and DTOs

## Overview
TypeScript interfaces, type definitions, enumeration constants, and UI label mappings representing domain entities, API request/response contracts, and UI view models across the application.

## Interface
- `ai.models.ts`: Types for AI chat and dashboard widgets (`ChatRequest`, `ChatMessage`, `ChatAnswer`, `ChartSpec`, `TableBlock`, `AnswerKpi`, `ConversationSummary`, `AiHealth`).
- `alert.models.ts`: Alert notification contracts (`Alert`, `AlertCount`).
- `auth.models.ts`: Authentication credentials and session tokens (`AuthResponse`, `LoginRequest`, `RegisterRequest`).
- `common.models.ts`: Shared API envelope and entity status types (`PagedResult`, `ApiError`, `Unit`, `ProductCategory`, `MarginStatus`, `OrderStatus`, `OrderType`, `PAGE_SIZE`).
- `cost.models.ts`: Fixed cost and operating fee contracts (`FixedCost`, `FixedCostRequest`, `Fees`).
- `ingredient.models.ts`: Ingredient inventory contracts (`Ingredient`, `IngredientRequest`, `IngredientUsages`).
- `kitchen.models.ts`: KDS queue contracts (`KitchenItem`, `KitchenQueueItem`, `KitchenCounts`, `KitchenQueueResponse`).
- `labels.ts`: Portuguese UI label dictionaries (`UNIT_LABELS`, `CATEGORY_LABELS`, `MARGIN_STATUS_LABELS`, `ORDER_STATUS_LABELS`, `ORDER_TYPE_LABELS`).
- `neighborhood.models.ts`: Delivery neighborhood and fee structures (`NeighborhoodDto`, `NeighborhoodInputDto`, `NeighborhoodRegion`).
- `order.models.ts`: Order management and public checkout DTOs (`OrderDto`, `CreateOrderInput`, `OrderTicketDto`, `PublicMenuDto`, `PublicOrderTrackingDto`).
- `preparation.models.ts`: Semi-finished preparation contracts (`Preparation`, `PreparationItemRequest`, `PreparationCalculation`).
- `pricing.models.ts`: Pricing engine calculation results (`PricingResult`, `MixedPricingResult`, `Combo`, `ComboPreviewResult`, `DashboardSummary`).
- `product.models.ts`: Product catalog and size definitions (`Product`, `ProductRequest`, `Size`, `SizeRequest`, `Beverage`).
- `profit.models.ts`: Profit reporting DTOs (`ProfitReportDto`, `ProfitByDayDto`, `ProfitByProductDto`, `ProfitByChannelDto`).
- `sheet.models.ts`: Technical sheet contracts (`SheetDetail`, `SheetItemRequest`, `SheetSaveRequest`, `PricingPreviewRequest`).
- `stock.models.ts`: Stock ledger contracts (`StockBalance`, `StockMovement`, `StockMovementRequest`, `StockReplenishmentReport`).
- `tenant.models.ts`: Pizzeria tenant profile (`TenantInfo`).
- `user.models.ts`: User management contracts and role definitions (`User`, `UserRole`, `USER_ROLES`, `ROLE_LABELS`, `CreateUserRequest`, `UpdateUserRequest`).

## Invariants
- `[common.models.ts:PAGE_SIZE]`: Default page size constant is 20 items.
- `[labels.ts]`: All user-facing enums map strictly to localized pt-BR string labels.

## Side Effects
- None

## Verification
- Run: `npm test --prefix frontend`

# PizzariaLucrativa.Application.DTOs

## Overview
Defines immutable data transfer objects (DTOs), input payloads, response models, and contract schemas shared across Application services, API controllers, and background tasks. Encompasses catalog, orders, pricing, kitchen production, analytics, AI assistant interactions, and stock replenishment contracts.

## Interface
- AI DTOs (`AiDtos.cs`): `ChatRequestDto`, `ChatMessageDto`, `ChatAnswerDto`, `AnswerBlockDto`, `ChartSpecDto`, `ChatCompletionRequest`, `ChatCompletionResult`
- Analytics DTOs (`AnalyticsDtos.cs`): `AnalyticsPeriodDto`, `OrdersByRegionDto`, `OrdersSummaryDto`, `SalesHeatmapDto`, `PeriodComparisonDto`, `NeighborhoodDto`
- Catalog DTOs (`CatalogDtos.cs`): `IngredientDto`, `PreparationDto`, `ProductDto`, `SizeDto`, `FixedCostDto`, `FeeConfigDto`, `BeverageDto`
- Order DTOs (`OrderDtos.cs`): `CreateOrderInput`, `OrderDto`, `KitchenQueueDto`, `OrderTicketDto`, `PublicMenuDto`, `PublicOrderTrackingDto`, `ProfitReportDto`
- Pricing DTOs (`PricingDtos.cs`): `TechnicalSheetDto`, `PricingResultDto`, `HalfHalfInput`, `MistaResultDto`, `ComboDto`, `DashboardSummaryDto`, `AlertDto`, `StockBalanceDto`
- Stock Replenishment DTOs (`StockReplenishmentDtos.cs`): `StockReplenishmentFilter`, `StockReplenishmentItemDto`, `StockReplenishmentReportDto`

## Invariants
- `[AiDtos:AnswerBlockDto]` Payload fields (`Text`, `Chart`, `Table`, `Kpis`) are mutually exclusive and strictly correspond to the declared `AnswerBlockType`.
- `[AiDtos:ChartSpecDto]` Chart series, data points, and labels are produced strictly from verified domain tool execution results, never by LLM hallucinations.
- `[OrderDtos:CreatePublicOrderInput]` Price and cost parameters cannot be supplied by clients; all monetary calculations are recomputed by the server from current catalog definitions.
- `[OrderDtos:PublicOrderTrackingDto]` Customer PII (such as full contact phone and detailed personal address) is omitted from public unauthenticated tracking endpoints.
- `[CatalogDtos:PreparationItemInput]` Item specifications require a reference to either a raw ingredient (`IngredientId`) or a sub-preparation (`SourcePreparationId`).

## Side Effects
- None (pure data contracts and schema definitions without side effects).

## Verification
- Run: `dotnet test`

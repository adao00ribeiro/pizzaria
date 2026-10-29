# PizzariaLucrativa.Application.Interfaces

## Overview
Declares abstract contracts, service interfaces, persistence abstractions, and tenant providers that decouple business orchestrations from infrastructure technologies (such as EF Core, ASP.NET Identity, and external AI providers).

## Interface
- Persistence & Context:
  - `IAppDbContext`: Database abstraction decoupling Application from EF Core (`Set<T>`, `Add<T>`, `Remove<T>`, `SaveChangesAsync`, `ExecuteInTransactionAsync`).
  - `ITenantProvider`: Ambient tenant resolver using `AsyncLocal` state (`TenantId`, `GetTenantId()`, `SetTenant()`, `Clear()`).
  - `ICurrentUser`: Ambient authenticated user contract (`UserId`, `Email`, `Role`, `TenantId`, `IsOwner`).
  - `IDateTimeProvider`: UTC clock abstraction for deterministic temporal calculations (`UtcNow`, `TodayLocal`, `TimeZone`).
  - `PagedResult<T>`, `PagedRequest` (`IPagedResult.cs`): Pagination contracts and helper struct.
- Domain Services & Operations:
  - `IIngredientService`, `IPreparationService`, `IProductService`, `ISizeService`, `IBeverageService`: Catalog lifecycle contracts.
  - `ITechnicalSheetService`, `IPricingService`, `IFixedCostService`, `IFeeService`, `IComboService`: Pricing and cost calculation contracts.
  - `IOrderService`, `IKitchenService`, `IStockService`, `IStockConsumptionService`, `IStockReplenishmentService`: Order lifecycle, kitchen kanban, stock deduction, and inventory replenishment contracts.
  - `IAlertService`, `IDashboardService`, `IAnalyticsService`, `IReportService`: KPI reporting, operational dashboard, and margin alert contracts.
  - `IRecalculationQueue`, `IRecalculationService`: Background recalculation pipeline contracts.
  - `IAuthService`, `ITenantService`, `IUserAdminService`: Tenant configuration and authentication contracts.
- AI Assistant Abstractions:
  - `IAiChatService`: High-level conversational assistant orchestrator.
  - `IAiConversationStore`: Tenant-scoped chat history persistence contract.
  - `IAiUsageService`: Token consumption and monthly budget enforcement contract.
  - `IChatModelClient`: Low-level LLM client contract (e.g., Google Gemini).

## Invariants
- `[ITenantProvider:GetTenantId]` Requires an active tenant context; throws an exception when invoked in an unauthenticated or missing tenant scope.
- `[IAppDbContext:Set]` All entity query sets must enforce multi-tenant isolation via underlying persistence configurations.
- `[IAiConversationStore:*]` All conversation message persistence and retrieval calls must be strictly isolated to the caller's tenant.
- `[IPagedResult:PagedRequest]` Enforces valid page numbering (>= 1) and bounded page sizes.

## Side Effects
- None (pure interface contracts).

## Verification
- Run: `dotnet test`

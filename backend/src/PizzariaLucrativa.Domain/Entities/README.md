# PizzariaLucrativa.Domain.Entities

## Overview
Domain entities representing core business concepts for the PizzariaLucrativa system. Provides POCO representations of tenants, products, technical sheets, nested recipe preparations, orders, stock movements, and AI conversation history, implementing multi-tenant scoping through base entity inheritance.

## Interface
- `AiConversation`: Multi-turn conversational session entity for the AI assistant (`[AiConversation.cs:AiConversation]`)
- `AiMessage`: Individual user or assistant message with token usage telemetry (`[AiMessage.cs:AiMessage]`)
- `AiUsageRecord`: Aggregated token consumption and estimated cost auditing entry (`[AiUsageRecord.cs:AiUsageRecord]`)
- `Alert`: Operational alert for low inventory or degraded profit margins (`[Alert.cs:Alert]`)
- `Combo`: Promotional product bundle with discounted or fixed pricing (`[Combo.cs:Combo]`)
- `ComboItem`: Individual product line item within a combo bundle (`[ComboItem.cs:ComboItem]`)
- `FeeConfig`: Tenant-level fee percentages and target margin configuration (`[FeeConfig.cs:FeeConfig]`)
- `FixedCost`: Recurring operational cost allocated across product sales (`[FixedCost.cs:FixedCost]`)
- `Ingredient`: Raw inventory material with purchase pricing and yield loss tracking (`[Ingredient.cs:Ingredient]`)
- `Neighborhood`: Delivery zone definition with region tagging and flat delivery fee (`[Neighborhood.cs:Neighborhood]`)
- `Order`: Counter, pickup, or delivery order aggregate root (`[Order.cs:Order]`)
- `OrderItem`: Line item in an order referencing a product and technical sheet (`[OrderItem.cs:OrderItem]`)
- `OrderItemFlavor`: Fractional flavor allocation for multi-flavor pizza items (`[OrderItemFlavor.cs:OrderItemFlavor]`)
- `Preparation`: Intermediate sub-recipe produced from raw ingredients or other preparations (`[Preparation.cs:Preparation]`)
- `PreparationItem`: Line item of an intermediate preparation recipe (`[PreparationItem.cs:PreparationItem]`)
- `Product`: Sellable catalog item associated with technical sheets (`[Product.cs:Product]`)
- `Size`: Pizza size categorization specifying maximum allowable flavors (`[Size.cs:Size]`)
- `StockMovement`: Inventory transaction logging stock increases, decrements, or adjustments (`[StockMovement.cs:StockMovement]`)
- `TechnicalSheet`: Product formulation sheet tracking direct costs and suggested prices (`[TechnicalSheet.cs:TechnicalSheet]`)
- `TechnicalSheetItem`: Ingredient or preparation component in a technical sheet (`[TechnicalSheetItem.cs:TechnicalSheetItem]`)
- `Tenant`: Top-level tenant account representing an independent pizzeria (`[Tenant.cs:Tenant]`)
- `TenantEntity`: Abstract base entity enforcing tenant scoping across business data (`[TenantEntity.cs:TenantEntity]`)

## Invariants
- Multi-tenant isolation: All domain business entities (except `Tenant` and `FeeConfig`) inherit from `[TenantEntity.cs:TenantEntity]`, requiring a valid non-empty `TenantId`.
- Primary key mapping: `[Tenant.cs:Tenant]` is the root entity and does not inherit from `TenantEntity`; `[FeeConfig.cs:FeeConfig]` uses `TenantId` directly as its primary key.
- Polymorphic preparation items: `[PreparationItem.cs:PreparationItem.Type]` dynamically resolves to `SheetItemType.Ingredient` when `IngredientId` is non-null, or `SheetItemType.Preparation` when `SourcePreparationId` is specified.
- Unique order codes: `[Order.cs:Order.Code]` holds a 6-character unambiguous alphanumeric code unique per tenant.
- Immutable temporal audit: `[TenantEntity.cs:TenantEntity.CreatedAt]` is initialized to UTC now upon instantiation.

## Side Effects
- None

## Verification
- Run: `dotnet test`

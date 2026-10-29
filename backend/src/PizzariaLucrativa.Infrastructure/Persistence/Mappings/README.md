# Persistence Mappings

## Overview
EF Core Fluent API configuration classes implementing `IEntityTypeConfiguration<T>` to configure database tables, column constraints, relations, cascade behavior, and indexes for domain and infrastructure entities.

## Interface
- `AiConversationMap`: Configures `AiConversations` table, column lengths, and user index (`[AiConversationMap.cs:AiConversationMap]`)
- `AiMessageMap`: Configures `AiMessages` table, relationships with `AiConversation`, and token metadata columns (`[AiMessageMap.cs:AiMessageMap]`)
- `AiUsageRecordMap`: Configures `AiUsageRecords` audit log table and indexes (`[AiUsageRecordMap.cs:AiUsageRecordMap]`)
- `AlertMap`: Configures `Alerts` table schema and read status constraints (`[AlertMap.cs:AlertMap]`)
- `AppUserMap`: Configures `AppUser` property constraints and tenant association (`[AppUserMap.cs:AppUserMap]`)
- `ComboItemMap`: Configures `ComboItems` junction table and cascading foreign keys (`[ComboItemMap.cs:ComboItemMap]`)
- `ComboMap`: Configures `Combos` table and child item relationships (`[ComboMap.cs:ComboMap]`)
- `FeeConfigMap`: Configures `FeeConfigs` precision and percentages (`[FeeConfigMap.cs:FeeConfigMap]`)
- `FixedCostMap`: Configures `FixedCosts` schema and category lengths (`[FixedCostMap.cs:FixedCostMap]`)
- `IngredientMap`: Configures `Ingredients` table and name length (`[IngredientMap.cs:IngredientMap]`)
- `NeighborhoodMap`: Configures `Neighborhoods` table, delivery fees, and active flags (`[NeighborhoodMap.cs:NeighborhoodMap]`)
- `OrderItemFlavorMap`: Configures `OrderItemFlavors` table and relations to order items and products (`[OrderItemFlavorMap.cs:OrderItemFlavorMap]`)
- `OrderItemMap`: Configures `OrderItems` table and cascade deletion from `Order` (`[OrderItemMap.cs:OrderItemMap]`)
- `OrderMap`: Configures `Orders` table, customer information lengths, indexes on Code, Status, PlacedAt, and relations (`[OrderMap.cs:OrderMap]`)
- `PreparationItemMap`: Configures `PreparationItems` table and ingredient references (`[PreparationItemMap.cs:PreparationItemMap]`)
- `PreparationMap`: Configures `Preparations` intermediate prep recipes and child items (`[PreparationMap.cs:PreparationMap]`)
- `ProductMap`: Configures `Products` table, categories, and technical sheet links (`[ProductMap.cs:ProductMap]`)
- `RefreshTokenMap`: Configures `RefreshToken` schema with unique index on `TokenHash` (`[RefreshTokenMap.cs:RefreshTokenMap]`)
- `SizeMap`: Configures `Sizes` table constraints (`[SizeMap.cs:SizeMap]`)
- `StockMovementMap`: Configures `StockMovements` ledger table and indexes (`[StockMovementMap.cs:StockMovementMap]`)
- `TechnicalSheetItemMap`: Configures `TechnicalSheetItems` table and cascading lifecycle (`[TechnicalSheetItemMap.cs:TechnicalSheetItemMap]`)
- `TechnicalSheetMap`: Configures `TechnicalSheets` table and recipe compositions (`[TechnicalSheetMap.cs:TechnicalSheetMap]`)
- `TenantMap`: Configures `Tenants` table with unique constraint on `Slug` (`[TenantMap.cs:TenantMap]`)

## Invariants
- Unique constraints: Unique index on `Tenant.Slug` (`[TenantMap.cs:Configure]`) and `RefreshToken.TokenHash` (`[RefreshTokenMap.cs:Configure]`)
- Cascade rules: Composite children (`Order.Items`, `TechnicalSheet.Items`, `Preparation.Items`, `Combo.Items`) specify `DeleteBehavior.Cascade` (`[OrderMap.cs:Configure]`)
- Null-preserving lookups: Optional reference relations like `Order.Neighborhood` specify `DeleteBehavior.SetNull` (`[OrderMap.cs:Configure]`)
- Pure declarative metadata: All mappings configure builders inside `Configure(EntityTypeBuilder<T>)` without maintaining state or executing queries (`[IngredientMap.cs:Configure]`)

## Side Effects
- None

## Verification
- Run: `dotnet test backend/`

# PizzariaLucrativa.Domain.Enums

## Overview
Enumerations defining domain classifications, lifecycles, measurement units, and operational modes across the pizzeria business logic.

## Interface
- `AiMessageRole`: Role of an AI chat participant (`User`, `Assistant`) (`[AiMessageRole.cs:AiMessageRole]`)
- `AlertStatus`: Resolution status of an operational alert (`Open`, `Resolved`) (`[AlertStatus.cs:AlertStatus]`)
- `AlertType`: Classification of operational alert (`MarginBelow`, `LowStock`) (`[AlertType.cs:AlertType]`)
- `AllocationType`: Overhead apportionment method (`PerPizza`, `RevenuePercentage`) (`[AllocationType.cs:AllocationType]`)
- `MarginStatus`: Profit margin compliance indicator (`Within`, `NeedsAdjustment`) (`[MarginStatus.cs:MarginStatus]`)
- `MeasureUnit`: Physical units of measurement (`g`, `ml`, `un`, `kg`, `L`, `porcao`) (`[MeasureUnit.cs:MeasureUnit]`)
- `NeighborhoodRegion`: Geographic macro-zone for delivery clustering (`Center`, `North`, `South`, `East`, `West`, `Other`) (`[NeighborhoodRegion.cs:NeighborhoodRegion]`)
- `OrderStatus`: Order progression lifecycle state (`Received`, `InPreparation`, `Ready`, `OutForDelivery`, `Completed`, `Cancelled`) (`[OrderStatus.cs:OrderStatus]`)
- `OrderType`: Origin channel of an order (`Counter`, `Pickup`, `Delivery`) (`[OrderType.cs:OrderType]`)
- `PaymentMethod`: Customer settlement instrument (`Cash`, `Card`, `Pix`) (`[PaymentMethod.cs:PaymentMethod]`)
- `ProductCategory`: High-level catalog taxonomy (`Pizza`, `Beverage`, `Other`) (`[ProductCategory.cs:ProductCategory]`)
- `SheetItemType`: Composition type of recipe items (`Ingredient`, `Preparation`) (`[SheetItemType.cs:SheetItemType]`)
- `StockMovementType`: Directional inventory transaction reason (`In`, `Out`, `Adjustment`, `OrderOut`) (`[StockMovementType.cs:StockMovementType]`)
- `StockReplenishmentUrgency`: Priority tier for stock reorders (`Critical`, `Low`, `Warning`, `Normal`) (`[StockReplenishmentUrgency.cs:StockReplenishmentUrgency]`)

## Invariants
- `[OrderStatus.cs:OrderStatus]`: Orders in state `Ready` trigger stock reduction; orders in state `Completed` are recognized in revenue reporting; orders in state `Cancelled` require a documented reason and stock reversal.
- `[MeasureUnit.cs:MeasureUnit]`: Mass units (`g`, `kg`) and volume units (`ml`, `L`) are mutually convertible only within their compatible metric dimension.
- `[AllocationType.cs:AllocationType]`: Dictates whether fixed costs divide by production volume (`PerPizza`) or scale as an overhead percentage (`RevenuePercentage`).

## Side Effects
- None

## Verification
- Run: `dotnet test`

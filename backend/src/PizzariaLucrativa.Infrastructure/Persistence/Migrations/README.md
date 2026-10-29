# Persistence Migrations

## Overview
EF Core database migrations tracking incremental schema changes and providing the model snapshot for relational persistence.

## Interface
- `InitialCreate`: Establishes the initial relational schema for identity, ingredients, products, fixed costs, and technical sheets (`[20261019154349_InitialCreate.cs:InitialCreate]`)
- `AddSlugTenants`: Adds unique slug column to the `Tenants` table (`[20261019171700_AddSlugTenants.cs:AddSlugTenants]`)
- `AddOrders`: Introduces tables for order lifecycle management (`Orders`, `OrderItems`, `OrderItemFlavors`) (`[20261019172711_AddOrders.cs:AddOrders]`)
- `AddProductImageUrl`: Adds `ImageUrl` column to `Products` table (`[20261020135222_AddProductImageUrl.cs:AddProductImageUrl]`)
- `AddNeighborhoodsAndAiHistory`: Adds `Neighborhoods`, `AiConversations`, `AiMessages`, and `AiUsageRecords` tables (`[20261026161958_AddNeighborhoodsAndAiHistory.cs:AddNeighborhoodsAndAiHistory]`)
- `AddBlocksJsonToAiMessage`: Adds `BlocksJson` column to `AiMessages` table (`[20261026221833_AddBlocksJsonToAiMessage.cs:AddBlocksJsonToAiMessage]`)
- `AppDbContextModelSnapshot`: Complete declarative metadata snapshot of the compiled EF Core entity model (`[AppDbContextModelSnapshot.cs:AppDbContextModelSnapshot]`)

## Invariants
- Bidirectional transitions: Each migration implements corresponding `Up(MigrationBuilder)` and `Down(MigrationBuilder)` transformations (`[20261026221833_AddBlocksJsonToAiMessage.cs:AddBlocksJsonToAiMessage]`)
- Chronological ordering: Migrations execute in timestamp prefix sequence matching EF Core metadata tracking
- Auto-generated metadata: Files are generated and updated via EF Core tools (`dotnet ef migrations`)

## Side Effects
- Modifies database schema tables, columns, constraints, foreign keys, and indexes via DDL statements when applied (`[20261019154349_InitialCreate.cs:Up]`)

## Verification
- Run: `dotnet test backend/`

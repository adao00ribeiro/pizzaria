# PizzariaLucrativa.Domain

## Overview
Core Domain layer of the PizzariaLucrativa architecture. Enforces enterprise business rules, multi-tenant isolation policies, financial pricing algorithms, inventory usage accounting, and domain aggregates completely isolated from external frameworks and data persistence mechanisms.

## Subdirectories & Boundaries
- [Entities](./Entities/README.md): Business entities and aggregates establishing multi-tenant entity hierarchies and operational models.
- [Enums](./Enums/README.md): Domain enumerations for order lifecycle stages, inventory operations, delivery regions, and measurement units.
- [Exceptions](./Exceptions/README.md): Domain-specific exception types for business rule infractions and sub-recipe cyclic dependencies.
- [Services](./Services/README.md): Pure domain calculation engines, pricing models, inventory usage estimators, tracking code generators, and unit converters.

## Verification
- Run: `dotnet test`

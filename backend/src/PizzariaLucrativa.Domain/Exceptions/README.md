# PizzariaLucrativa.Domain.Exceptions

## Overview
Domain-specific exceptions signaling business rule violations and recursive structure invalidations across the domain layer.

## Interface
- `DomainException`: Base domain exception representing business rule violations mapped to HTTP 400 Bad Request at the API boundary (`[DomainException.cs:DomainException]`)
- `CycleDetectedException`: Specialized exception raised when circular dependencies are detected in nested sub-recipe preparations (`[CycleDetectedException.cs:CycleDetectedException]`)

## Invariants
- `[DomainException.cs:DomainException]`: Raised exclusively for business validation failures, encapsulating localized, actionable messages without exposing technical stack traces.
- `[CycleDetectedException.cs:CycleDetectedException.Trail]`: Contains an immutable list of visited `Guid` identifiers indicating the cyclic loop path.

## Side Effects
- None

## Verification
- Run: `dotnet test`

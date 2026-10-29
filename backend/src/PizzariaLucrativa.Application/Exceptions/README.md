# PizzariaLucrativa.Application.Exceptions

## Overview
Declares standardized application-level domain and execution exceptions. These exceptions translate operational, authentication, validation, and relational integrity failures into structured HTTP status responses in upper layers.

## Interface
- `ValidationException`: Raised when request payloads violate business or input validation rules; carries a list of field-detail error tuples (`Errors`).
- `ConflictException`: Raised when an operation violates relational constraints or business state rules (such as deleting an ingredient or preparation referenced by active technical sheets).
- `NotFoundException`: Raised when a requested resource or tenant-scoped entity cannot be found.
- `UnauthorizedException`: Raised when credentials are invalid, tokens are expired, or tenant context is missing.

## Invariants
- `[AppExceptions:ValidationException]` Contains at least one validation failure tuple `(string Field, string Detail)`.
- `[AppExceptions:*]` All exceptions inherit from `System.Exception` and encapsulate domain/application error messages without exposing raw infrastructure stack traces.

## Side Effects
- None (pure exception definitions).

## Verification
- Run: `dotnet test`

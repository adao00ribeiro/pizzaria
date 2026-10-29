# Filters Package

## Overview
Houses action filter components that enforce cross-cutting request validation before action execution.

## Interface
- `ValidationFilter` [`ValidationFilter.cs:ValidationFilter`]: `IAsyncActionFilter` that dynamically discovers and executes registered FluentValidation validators for each action parameter.

## Invariants
- `[ValidationFilter.cs:ValidationFilter]` Inspects every non-null action argument in `context.ActionArguments`.
- `[ValidationFilter.cs:ValidationFilter]` Throws `FluentValidation.ValidationException` immediately when validation errors occur, preventing invalid payloads from reaching application services.

## Side Effects
- Short-circuits action pipeline execution by throwing `FluentValidation.ValidationException` on invalid payloads.

## Verification
- Run: `dotnet test`

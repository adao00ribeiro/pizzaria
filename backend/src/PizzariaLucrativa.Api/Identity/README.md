# Identity Package

## Overview
Implements application user context abstractions by inspecting active HTTP requests and JWT principal claims.

## Interface
- `HttpContextCurrentUser` [`HttpContextCurrentUser.cs:HttpContextCurrentUser`]: Implementation of `ICurrentUser` resolving `UserId` from the current `HttpContext.User`.

## Invariants
- `[HttpContextCurrentUser.cs:HttpContextCurrentUser]` Reads `UserId` from `ClaimTypes.NameIdentifier` first, falling back to raw `sub` claim.
- `[HttpContextCurrentUser.cs:HttpContextCurrentUser]` Returns `null` if the user is unauthenticated or has no identifier claims.

## Side Effects
- None

## Verification
- Run: `dotnet test`

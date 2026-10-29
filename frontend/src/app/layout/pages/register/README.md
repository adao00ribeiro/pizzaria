# Register Page

## Overview
Self-service tenant signup page capturing pizzeria business name, owner personal details, credentials, and password confirmation.

## Interface
- `register.component.ts:RegisterComponent`: Standalone Angular component managing user registration forms and account creation.

## Invariants
- `[register.component.ts:RegisterComponent]`: Validates that password and confirmation password fields match exactly before allowing submission.

## Side Effects
- `[register.component.ts:RegisterComponent]`: Dispatches registration request via `AuthService.register()` and navigates to the dashboard upon completion.

## Verification
- Run: `npm test --prefix frontend`

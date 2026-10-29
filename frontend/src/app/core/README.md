# Core Module

## Overview
Foundational application infrastructure containing stateful services, security guards, HTTP interceptors, typed models, and reactive form utilities.

## Interface
- `forms.compat.ts:NonNullableFormControl`: Typed constructor wrapper guaranteeing non-nullable form control value semantics.
- `forms.compat.ts:NonNullableFormGroup`: Typed constructor wrapper guaranteeing non-nullable form group control structures.
- [Route Guards](./guards/README.md): Authentication, role authorization, and tenant preloading guards.
- [HTTP Interceptors](./interceptors/README.md): JWT bearer injection, silent error contexts, and Portuguese error toast notifications.
- [Domain Models & DTOs](./models/README.md): Shared interfaces, types, constants, and localized label maps.
- [Application Services](./services/README.md): Singleton services for authentication, PWA updates, storage, tenants, and themes.
- [Core Utilities](./utils/README.md): Normalization helpers for paginated or unpaginated API results.

## Invariants
- `[forms.compat.ts:NonNullableFormControl]`: Automatically sets `{ nonNullable: true }` in control options upon initialization.

## Side Effects
- None directly in the root module files; side effects are isolated within subpackages.

## Verification
- Run: `npm test --prefix frontend`

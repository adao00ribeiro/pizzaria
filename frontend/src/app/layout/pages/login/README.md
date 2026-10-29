# Login Page

## Overview
Authentication entry page supporting user credential validation, error feedback in Portuguese, and theme toggle.

## Interface
- `login.component.ts:LoginComponent`: Standalone Angular component managing the email and password reactive form and login submission.

## Invariants
- `[login.component.ts:LoginComponent]`: Disables submit action while request is in flight and displays progress indicator.

## Side Effects
- `[login.component.ts:LoginComponent]`: Invokes `AuthService.login()` and redirects on success to the role-specific home route.

## Verification
- Run: `npm test --prefix frontend`

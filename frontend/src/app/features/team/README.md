# Team Management Feature

## Overview
User administration module for inviting and managing pizzeria staff members, granting role-based permissions (`owner`, `gerente`, `atendente`, `pizzaiolo`, `entregador`), and resetting passwords.

## Interface
- `team.page.ts:TeamPage`: User table displaying registered pizzeria collaborators, assigned roles, and active status.
- `team.service.ts:TeamService`: HTTP service managing employee records via `/users`.
- `user-form.dialog.ts:UserFormDialog`: Dialog modal for registering new staff or updating existing member details and roles.
- `user-form.dialog.ts:UserDialogData`: Injection configuration for user edit dialog.

## Invariants
- `[team.service.ts:TeamService]`: Only tenant owners can assign or modify administrative roles.

## Side Effects
- `[team.service.ts:TeamService]`: Dispatches HTTP GET, POST, PUT, and DELETE calls to `/users`.

## Verification
- Run: `npm test --prefix frontend`

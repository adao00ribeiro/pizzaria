# Application Shell Layout

## Overview
Primary navigation container hosting responsive toolbar header, collapsible sidenav drawer, role-aware menu entries, theme toggles, and live badge counters for alerts and critical stock.

## Interface
- `shell.component.ts:ShellComponent`: Root shell container component managing layout breakpoints, tenant branding, navigation links, and logout flow.

## Invariants
- `[shell.component.ts:ShellComponent]`: Automatically filters navigation links based on current user role permissions (e.g. hides financials from pizzaiolo).
- `[shell.component.ts:ShellComponent]`: Automatically closes mobile sidenav upon route transition completion.

## Side Effects
- `[shell.component.ts:ShellComponent]`: Subscribes to `BreakpointObserver` and `Router.events`.
- `[shell.component.ts:ShellComponent]`: Periodically polls or queries stock and alert counts.

## Verification
- Run: `npm test --prefix frontend`

# System Alerts Feature

## Overview
Management interface for operational alerts, including stock deficiency warnings, margin slippage, and pending kitchen queue notifications.

## Interface
- `alerts.page.ts:AlertsPage`: Routed page displaying filtered alert feeds with resolution actions and badge counts.
- `alerts.service.ts:AlertsService`: HTTP client service communicating with `/alerts` to list, count, and resolve alerts.

## Invariants
- `[alerts.service.ts:AlertsService]`: Exposes reactive count signals to keep navigation shell notification badges in sync with unread alerts.

## Side Effects
- `[alerts.service.ts:AlertsService]`: Triggers HTTP GET, POST, and PUT calls to `/alerts`.

## Verification
- Run: `npm test --prefix frontend`

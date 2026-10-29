# Shared UI Components

## Overview
Reusable Angular standalone components and form directives providing common modal dialogs, status badges, formatted currency inputs, and calculation KPI cards.

## Interface
- `confirm-dialog.ts:ConfirmDialog`: Generic confirmation modal with configurable title, message, and action button labels.
- `confirm-dialog.ts:ConfirmDialogData`: Interface payload for opening confirmation dialogs.
- `margin-status-chip.ts:MarginStatusChip`: Status badge component displaying "Dentro da margem" (green) or "Precisa ajustar" (red).
- `money-input.ts:MoneyInputDirective`: Reactive form directive implementing `ControlValueAccessor` for currency formatting and input parsing.
- `money-input.ts:formatBrl`: Pure helper formatting numeric values as Brazilian Real currency strings (`R$ 32,90`).
- `money-input.ts:parseMoney`: Pure helper parsing user input text into valid numeric currency values.
- `pricing-result-cards.ts:PricingResultCards`: KPI summary card grid displaying direct cost, suggested price, gross profit, and margin percentage.

## Invariants
- `[money-input.ts:parseMoney]`: Gracefully parses both comma and dot decimal separators without throwing exceptions.
- `[pricing-result-cards.ts:PricingResultCards]`: Only renders metric cards whose underlying input values are defined.

## Side Effects
- None

## Verification
- Run: `npm test --prefix frontend`

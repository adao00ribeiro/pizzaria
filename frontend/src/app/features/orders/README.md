# Orders Management Feature

## Overview
POS order management interface covering manual order creation, multi-flavor pizza builder, thermal receipt printing, and order status lifecycle.

## Interface
- `orders.page.ts:OrdersPage`: Main order list showing filterable order streams with inline actions and status indicators.
- `orders.service.ts:OrdersService`: HTTP service coordinating order creation, queries, cancellations, and ticket fetching via `/orders`.
- `orders.service.ts:OrderFilterParams`: Query parameter model for filtering orders by status, type, and dates.
- `order-ticket.page.ts:OrderTicketPage`: Thermal receipt view configured for 80mm/58mm printing.
- `new-order.dialog.ts:NewOrderDialog`: Comprehensive dialog wizard for building pizzas, adding drinks, selecting delivery zones, and computing totals.

## Invariants
- `[orders.service.ts:OrdersService]`: Cancelling orders requires a documented reason and automatically prompts for inventory restocking.

## Side Effects
- `[orders.service.ts:OrdersService]`: Dispatches HTTP calls to `/orders`.
- `[order-ticket.page.ts:OrderTicketPage]`: Triggers browser print dialog via `window.print()`.

## Verification
- Run: `npm test --prefix frontend`

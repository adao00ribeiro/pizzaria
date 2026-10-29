# Kitchen Display System (KDS) Feature

## Overview
Real-time Kitchen Display System providing pizza preparation order queues, recipe reference expansion, elapsed timer alerts, and auditory order notifications.

## Interface
- `kitchen.page.ts:KitchenPage`: KDS dashboard component showing pending tickets, status toggles, elapsed timers, and detail panels.
- `kitchen.page.ts:TimerSeverity`: Urgency classification type for order ticket preparation time.
- `kitchen.service.ts:KitchenService`: HTTP service polling and mutating kitchen orders via `/kitchen`.
- `kitchen-sound.service.ts:KitchenSoundService`: Service synthesizing audio chimes via Web Audio API when new orders enter the queue.
- `cancel-order.dialog.ts:CancelOrderDialog`: Modal dialog prompting for cancellation reasons and restock options.

## Invariants
- `[kitchen.service.ts:KitchenService]`: Orders transition linearly through kitchen states (`Received` -> `Preparing` -> `Ready`).
- `[kitchen-sound.service.ts:KitchenSoundService]`: Gracefully handles muted or blocked audio contexts until user interaction occurs.

## Side Effects
- `[kitchen.service.ts:KitchenService]`: Dispatches HTTP calls to `/kitchen/queue` and status update endpoints.
- `[kitchen-sound.service.ts:KitchenSoundService]`: Generates synthesized tones using the browser `AudioContext`.

## Verification
- Run: `npm test --prefix frontend`

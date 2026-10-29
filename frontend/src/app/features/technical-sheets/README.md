# Technical Sheets Feature

## Overview
Technical recipe engineering module calculating direct cost of goods sold (CMV), ingredient proportions by pizza size, preparation links, and suggested menu prices.

## Interface
- `sheets.page.ts:SheetsPage`: Overview list of all product technical sheets with direct costs and target margin indicators.
- `sheet-editor.page.ts:SheetEditorPage`: Interactive sheet composition workbench supporting ingredient addition, portion scaling, preparation inclusion, and instant margin calculation.
- `sheets.service.ts:SheetsService`: HTTP client handling technical sheet persistence and pricing simulations via `/sheets` and `/pricing/preview`.

## Invariants
- `[sheet-editor.page.ts:SheetEditorPage]`: Recalculates total direct costs and margin status reactively whenever recipe item weights or ingredient unit prices change.

## Side Effects
- `[sheets.service.ts:SheetsService]`: Dispatches HTTP calls to `/sheets` and `/pricing/preview`.

## Verification
- Run: `npm test --prefix frontend`

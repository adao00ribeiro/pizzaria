# Shared Template Pipes

## Overview
Standalone pure Angular pipes for formatting financial values and percentage ratios in template interpolations according to Brazilian locale conventions.

## Interface
- `brl.pipe.ts:BrlPipe`: Formats numeric amounts into localized BRL currency format (`R$ 13,94`), outputting an em dash (`—`) for nullish values.
- `pct.pipe.ts:PctPipe`: Formats numeric ratio values into percentage strings (`60,0%`), outputting an em dash (`—`) for nullish values.

## Invariants
- `[brl.pipe.ts:BrlPipe, pct.pipe.ts:PctPipe]`: Pure pipes that return `"—"` whenever input values are `null`, `undefined`, or `NaN`.

## Side Effects
- None

## Verification
- Run: `npm test --prefix frontend`

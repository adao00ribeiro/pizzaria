# Profit and Loss Reporting Feature

## Overview
Financial reporting interface breaking down gross revenue, variable ingredient costs, delivery expenses, and net profit margins across date intervals and sales channels.

## Interface
- `profit.page.ts:ProfitPage`: Analytics dashboard rendering period presets, DRE profit summaries, and breakdowns by product and channel.
- `profit.page.ts:PeriodPreset`: Date filter presets ("today", "yesterday", "7days", "30days", "thisMonth").
- `profit.service.ts:ProfitService`: HTTP service fetching financial aggregations via `/profit/report`.

## Invariants
- `[profit.page.ts:ProfitPage]`: Renders Chart.js visuals with automatic light/dark theme color adjustments.

## Side Effects
- `[profit.service.ts:ProfitService]`: Dispatches HTTP calls to `/profit/report`.
- `[profit.page.ts:ProfitPage]`: Manipulates HTML5 `<canvas>` elements to display DRE charts.

## Verification
- Run: `npm test --prefix frontend`

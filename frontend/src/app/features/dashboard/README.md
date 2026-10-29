# Executive Dashboard Feature

## Overview
Executive KPI overview providing sales metrics, product profit rankings, margin trend charts, and operational summary cards.

## Interface
- `dashboard.page.ts:DashboardPage`: Executive dashboard component orchestrating summary metrics, top-seller rankings, and Chart.js graphs.
- `dashboard.page.ts:ChartViewMode`: View mode type for margin vs sales volume breakdowns.
- `dashboard.page.ts:RankingSource`: Data source toggle for product sales rankings.
- `dashboard.page.ts:ChartSource`: Filter mode for revenue and margin charts.
- `dashboard.service.ts:DashboardService`: HTTP client service retrieving aggregated summary figures from `/pricing/dashboard`.

## Invariants
- `[dashboard.page.ts:DashboardPage]`: Destroys active Chart.js canvas instances before redrawing on filter or theme changes.

## Side Effects
- `[dashboard.service.ts:DashboardService]`: Fetches aggregated analytics via HTTP GET `/pricing/dashboard`.
- `[dashboard.page.ts:DashboardPage]`: Mutates HTML5 `<canvas>` elements to display charts.

## Verification
- Run: `npm test --prefix frontend`

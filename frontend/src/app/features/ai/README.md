# AI Assistant Feature

## Overview
Interactive conversational intelligence interface allowing managers to query business performance, simulate pricing scenarios, and inspect structured analytical cards (KPIs, tables, and Chart.js visualizations).

## Interface
- `ai.page.ts:AiPage`: Primary chat page coordinating message submission, suggested prompts, and response streaming.
- `ai.service.ts:AiService`: HTTP client service interacting with `/ai/chat`, `/ai/conversations`, `/ai/health`, and `/ai/feedback` endpoints.
- `ai-history.component.ts:AiHistoryComponent`: Sidenav/dialog displaying prior conversation histories and loading sessions.
- `chat-message.component.ts:ChatMessageComponent`: Renders individual assistant/user chat bubbles with support for structured card blocks.
- `chat-message.component.ts:UiChatMessage`: UI model wrapping chat messages, loading states, and error alerts.
- `kpi-block.component.ts:KpiBlockComponent`: Renders highlight metric widgets returned by the AI agent.
- `table-block.component.ts:TableBlockComponent`: Renders tabular data returned by the AI agent.
- `chart-block.component.ts:ChartBlockComponent`: Renders dynamic bar/line/pie charts using Chart.js based on AI specifications.

## Invariants
- `[chat-message.component.ts:ChatMessageComponent]`: Assistant messages render structured blocks (`kpi`, `table`, `chart`) only when explicitly returned by the API payload.
- `[chart-block.component.ts:ChartBlockComponent]`: Destroys previous Chart.js instances before re-rendering new data to prevent memory leaks and canvas conflicts.

## Side Effects
- `[ai.service.ts:AiService]`: Dispatches HTTP calls to `/ai/*` backend endpoints.
- `[chart-block.component.ts:ChartBlockComponent]`: Renders graphics onto HTML5 `<canvas>` elements.

## Verification
- Run: `npm test --prefix frontend`

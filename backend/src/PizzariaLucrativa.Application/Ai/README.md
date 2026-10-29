# PizzariaLucrativa.Application.Ai

## Overview
Provides core abstractions, system prompt generation, tool definitions, tool registry, and secure execution mechanisms for Google Gemini function calling in the AI assistant subsystem. It enforces security guardrails against prompt injection, prevents personal identifiable information (PII) leakage, and restricts model responses to validated domain tool results.

## Interface
- `AiPromptBuilder`: Constructs localized system prompts with business rules and wraps user queries in `<user_query>` protection tags (`PromptVersion = "v1.0"`).
- `AiToolDefinitions`: Static declarations (`ChatToolDeclaration`) for allowed Gemini tools: `GetProfitSummary`, `GetProfitByDay`, `GetProfitByProduct`, `GetProfitByChannel`, `GetOrdersByRegion`, `GetOrdersSummary`, `GetSalesHeatmap`, `ComparePeriods`, `GetStockAlerts`, and `All`.
- `AiToolExecutor`: Dispatches and executes validated tool calls against application services (`IReportService`, `IAnalyticsService`, `IStockService`) with parameter validation and structured error handling.
- `AiToolRegistry`: In-memory registry and allowlist validator for tool declarations available to the language model.

## Invariants
- `[AiPromptBuilder:BuildSystemPrompt]` The reference date is anchored to the Brazilian timezone (`America/Sao_Paulo` or `E. South America Standard Time`).
- `[AiPromptBuilder:WrapUserQuery]` Closing `</user_query>` tags in user input are stripped to prevent tag escape and prompt injection.
- `[AiToolDefinitions:*]` All tool schemas enforce strict parameters with `additionalProperties: false`, omitting tenant identifiers and raw SQL.
- `[AiToolExecutor:TryGetDateRange]` Date intervals cannot have end dates earlier than start dates, and time ranges cannot exceed 366 days.
- `[AiToolExecutor:ExecuteAsync]` Unhandled service exceptions are caught, logged as warnings, and returned as serialized `{ error: ... }` payloads without leaking sensitive infrastructure details.
- `[AiToolRegistry:IsAllowed]` Tool lookup is strictly evaluated case-insensitively against the declared allowlist.

## Side Effects
- `[AiToolExecutor:ExecuteAsync]` Queries operational and financial data through `IReportService`, `IAnalyticsService`, and `IStockService`.
- `[AiToolExecutor:ExecuteAsync]` Writes warning logs through `ILogger<AiToolExecutor>` when execution errors occur.

## Verification
- Run: `dotnet test`

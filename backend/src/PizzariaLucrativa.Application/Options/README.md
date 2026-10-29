# PizzariaLucrativa.Application.Options

## Overview
Encapsulates strongly-typed configuration options and validation logic for the AI assistant subsystem, binding to the `Ai` configuration section in `appsettings.json`.

## Interface
- `AiOptions`: Top-level configuration class specifying model settings, API credentials, endpoints, rollout percentage, budget parameters, and history settings (`SectionName = "Ai"`).
  - `Validate()`: Performs fail-fast validation of required AI configuration values at startup.
- `AiBudgetOptions`: Tenant-level spending limits, enforcement flags, and alert thresholds (`MonthlyUsdPerTenant`, `Enforce`, `AlertPercentage`).
- `AiHistoryOptions`: Conversation history persistence settings, retention window in days, and maximum messages per thread (`Enabled`, `RetentionDays`, `MaxMessagesPerConversation`, `StoreToolResults`).

## Invariants
- `[AiOptions:Validate]` When `Enabled` is true, `ApiKey` must be non-empty, and `Provider` must match `"Gemini"` (case-insensitive).
- `[AiOptions:Validate]` `RolloutPercentage` must be between 0 and 100 inclusive.
- `[AiOptions:Validate]` When `Budget.Enforce` is active, `Budget.MonthlyUsdPerTenant` must be greater than zero.
- `[AiOptions:Validate]` `MaxToolRounds` and `TimeoutSeconds` must be greater than zero.

## Side Effects
- None (pure option classes and validation routines).

## Verification
- Run: `dotnet test`

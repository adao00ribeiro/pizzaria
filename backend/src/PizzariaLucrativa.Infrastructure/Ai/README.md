# Ai Infrastructure

## Overview
Provides external AI infrastructure implementations for Google Gemini Developer API integration, multi-tenant conversational history persistence, and background conversation retention cleanup.

## Interface
- `AiConversationStore`: Implements `IAiConversationStore` for managing conversation lifecycles and persisting messages to EF Core (`[AiConversationStore.cs:AiConversationStore]`)
- `GeminiChatModelClient`: Implements `IChatModelClient` providing HTTP-based communication, payload formatting, tool declarations sanitization, and retry policies for Google Gemini models (`[GeminiChatModelClient.cs:GeminiChatModelClient]`)
- `AiHistoryCleanupService`: Background service (`BackgroundService`) executing periodic purging of expired AI conversation records based on configured retention policies (`[AiHistoryCleanupService.cs:AiHistoryCleanupService]`)
- `AiOptions`, `AiBudgetOptions`, `AiHistoryOptions`: Global type aliases re-exporting application options (`[AiOptions.cs]`)

## Invariants
- API key security: The Gemini API key is transmitted via `x-goog-api-key` header and never written to log streams (`[GeminiChatModelClient.cs:CompleteAsync]`)
- Multi-tenant isolation: Conversation queries enforce tenant boundaries through `AppDbContext` global query filters unless explicitly purged via background worker (`[AiConversationStore.cs:GetAsync]`, `[AiHistoryCleanupService.cs:RunCleanupAsync]`)
- Schema compatibility: Gemini function calling schemas strip unsupported keywords (`additionalProperties`, `$schema`, `$id`) to prevent 400 Bad Request responses (`[GeminiChatModelClient.cs:CleanParametersForGemini]`)
- Feature flag guard: If `AiOptions.History.Enabled` is `false`, storage operations return in-memory transient results without persisting to the database (`[AiConversationStore.cs:GetOrCreateAsync]`)
- Transient retry policy: Gemini calls automatically retry up to 2 times only on HTTP 429 and 5xx responses with linear backoff (`[GeminiChatModelClient.cs:CompleteAsync]`)

## Side Effects
- External outbound HTTPS calls to Google Gemini API (`/v1beta/models/{Model}:generateContent`) (`[GeminiChatModelClient.cs:CompleteAsync]`)
- Persists and deletes records in `AiConversations` and `AiMessages` tables (`[AiConversationStore.cs:AppendMessageAsync]`, `[AiHistoryCleanupService.cs:RunCleanupAsync]`)

## Verification
- Run: `dotnet test backend/`

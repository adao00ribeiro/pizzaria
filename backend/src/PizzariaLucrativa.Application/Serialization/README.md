# PizzariaLucrativa.Application.Serialization

## Overview
Provides custom System.Text.Json converters to ensure contract stability between backend services, frontend visualization components, and Google Gemini function-calling payloads.

## Interface
- `LowerCaseEnumConverter<TEnum>`: Generic JSON converter (`JsonConverter<TEnum>`) that serializes enum identifiers into camelCase string representations (e.g., "bar", "line", "currency") and deserializes case-insensitively.

## Invariants
- `[LowerCaseEnumConverter:Write]` Serializes values by applying `JsonNamingPolicy.CamelCase` to the enum identifier string.
- `[LowerCaseEnumConverter:Read]` Validates incoming string tokens case-insensitively, throwing a `JsonException` on unrecognized values.

## Side Effects
- None (in-memory JSON transformation utility).

## Verification
- Run: `dotnet test`

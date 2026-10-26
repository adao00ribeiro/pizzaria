using System.Text.Json.Nodes;
using PizzariaLucrativa.Application.Ai;
using PizzariaLucrativa.Application.DTOs;
using PizzariaLucrativa.Infrastructure.Ai;

namespace PizzariaLucrativa.Api.Tests.Ai;

public sealed class GeminiPayloadTests
{
    [Fact]
    public void BuildGeminiPayload_WithRegisteredTools_RemovesAdditionalPropertiesFromAllDeclarations()
    {
        var request = new ChatCompletionRequest(
            SystemPrompt: "Você é um assistente financeiro.",
            Messages: new[] { new ChatModelMessage("user", "Quanto lucrei esse mês?") },
            Tools: AiToolDefinitions.All,
            MaxOutputTokens: 1024,
            Temperature: 0.2
        );

        var payload = GeminiChatModelClient.BuildGeminiPayload(request);

        var payloadJson = payload.ToJsonString();

        // Gemini REST API rejects 'additionalProperties' in Schema proto
        Assert.DoesNotContain("additionalProperties", payloadJson);
        Assert.DoesNotContain("$schema", payloadJson);

        // All 9 tools must still be present with their names, descriptions, and parameters
        var toolsNode = payload["tools"] as JsonArray;
        Assert.NotNull(toolsNode);
        Assert.Single(toolsNode);

        var declarations = toolsNode[0]?["functionDeclarations"] as JsonArray;
        Assert.NotNull(declarations);
        Assert.Equal(9, declarations.Count);

        foreach (var declNode in declarations)
        {
            var decl = declNode as JsonObject;
            Assert.NotNull(decl);
            Assert.False(string.IsNullOrWhiteSpace(decl["name"]?.ToString()));
            Assert.False(string.IsNullOrWhiteSpace(decl["description"]?.ToString()));

            var parameters = decl["parameters"] as JsonObject;
            Assert.NotNull(parameters);
            Assert.Equal("object", parameters["type"]?.ToString());
            Assert.NotNull(parameters["properties"]);
            Assert.False(parameters.ContainsKey("additionalProperties"));
        }
    }

    [Fact]
    public void CleanParametersForGemini_NestedObjectAndArray_StripsUnsupportedFieldsRecursively()
    {
        var input = JsonNode.Parse("""
        {
          "$schema": "http://json-schema.org/draft-07/schema#",
          "$id": "https://example.com/schema.json",
          "type": "object",
          "properties": {
            "filters": {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "tags": {
                  "type": "array",
                  "items": {
                    "type": "object",
                    "additionalProperties": false,
                    "properties": {
                      "name": { "type": "string" }
                    }
                  }
                }
              }
            }
          },
          "additionalProperties": false
        }
        """);

        var cleaned = GeminiChatModelClient.CleanParametersForGemini(input);
        var json = cleaned?.ToJsonString();

        Assert.NotNull(json);
        Assert.DoesNotContain("additionalProperties", json);
        Assert.DoesNotContain("$schema", json);
        Assert.DoesNotContain("$id", json);
        Assert.Contains("filters", json);
        Assert.Contains("tags", json);
        Assert.Contains("name", json);
    }

    [Fact]
    public void BuildGeminiPayload_WithToolCallAndResponse_UsesUserRoleForResponseAndPreservesThoughtSignature()
    {
        using var argsDoc = System.Text.Json.JsonDocument.Parse("""{"from":"2026-09-01","to":"2026-09-30"}""");
        using var respDoc = System.Text.Json.JsonDocument.Parse("""{"netProfit":5478.38}""");

        var toolCall = new ChatToolCall(
            "call_12345",
            "get_profit_summary",
            argsDoc.RootElement.Clone(),
            ThoughtSignature: "sig_abcde123"
        );

        var request = new ChatCompletionRequest(
            SystemPrompt: "Você é um assistente financeiro.",
            Messages: new[]
            {
                new ChatModelMessage("user", "Quanto lucrei esse mês?"),
                new ChatModelMessage("model", null, new[] { toolCall }),
                new ChatModelMessage("function", ToolName: "get_profit_summary", ToolResponse: respDoc.RootElement.Clone())
            },
            Tools: AiToolDefinitions.All
        );

        var payload = GeminiChatModelClient.BuildGeminiPayload(request);
        var contents = payload["contents"] as JsonArray;

        Assert.NotNull(contents);
        Assert.Equal(3, contents.Count);

        // Turn 1: user
        Assert.Equal("user", contents[0]?["role"]?.ToString());

        // Turn 2: model with tool call and thoughtSignature
        var modelTurn = contents[1] as JsonObject;
        Assert.NotNull(modelTurn);
        Assert.Equal("model", modelTurn["role"]?.ToString());

        var modelParts = modelTurn["parts"] as JsonArray;
        Assert.NotNull(modelParts);
        var fcPart = modelParts[0] as JsonObject;
        Assert.NotNull(fcPart);
        Assert.Equal("sig_abcde123", fcPart["thoughtSignature"]?.ToString());

        var fc = fcPart["functionCall"] as JsonObject;
        Assert.NotNull(fc);
        Assert.Equal("call_12345", fc["id"]?.ToString());
        Assert.Equal("get_profit_summary", fc["name"]?.ToString());

        // Turn 3: function response must have role: 'user' in Gemini REST API
        var respTurn = contents[2] as JsonObject;
        Assert.NotNull(respTurn);
        Assert.Equal("user", respTurn["role"]?.ToString());

        var respParts = respTurn["parts"] as JsonArray;
        Assert.NotNull(respParts);
        var funcRespPart = respParts[0] as JsonObject;
        Assert.NotNull(funcRespPart);
        var funcResp = funcRespPart["functionResponse"] as JsonObject;
        Assert.NotNull(funcResp);
        Assert.Equal("get_profit_summary", funcResp["name"]?.ToString());
    }
}

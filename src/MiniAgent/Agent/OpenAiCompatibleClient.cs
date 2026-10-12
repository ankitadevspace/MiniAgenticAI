using System.Text.Json;
using System.Text.Json.Nodes;

namespace MiniAgent.Agent;

/// <summary>
/// Client for the OpenAI-style /chat/completions API with tool calling.
/// Works with Ollama (free, local), Groq, OpenRouter, Gemini's OpenAI-compatible
/// endpoint, and OpenAI itself - only the base URL, model and key change.
/// </summary>
public sealed class OpenAiCompatibleClient : ILlmClient
{
    private readonly HttpClient _http;
    private readonly string _url;
    private readonly string _model;
    private readonly string? _apiKey;
    private readonly int _maxTokens;

    public OpenAiCompatibleClient(HttpClient http, string baseUrl, string model, string? apiKey, int maxTokens = 1024)
    {
        _http = http;
        _url = baseUrl.TrimEnd('/') + "/chat/completions";
        _model = model;
        _apiKey = apiKey;
        _maxTokens = maxTokens;
    }

    public async Task<LlmReply> SendAsync(
        string system,
        IReadOnlyList<ToolSpec> tools,
        IReadOnlyList<ChatMessage> history,
        CancellationToken ct = default)
    {
        var messages = new JsonArray
        {
            new JsonObject { ["role"] = "system", ["content"] = system }
        };

        foreach (var message in history)
        {
            switch (message)
            {
                case UserMessage user:
                    messages.Add(new JsonObject { ["role"] = "user", ["content"] = user.Text });
                    break;

                case AssistantMessage assistant:
                    var entry = new JsonObject { ["role"] = "assistant" };
                    JsonNode? text = assistant.Text.Length > 0 ? JsonValue.Create(assistant.Text) : null;
                    entry["content"] = text;
                    if (assistant.ToolCalls.Count > 0)
                    {
                        var calls = new JsonArray();
                        foreach (var call in assistant.ToolCalls)
                        {
                            calls.Add(new JsonObject
                            {
                                ["id"] = call.Id,
                                ["type"] = "function",
                                ["function"] = new JsonObject
                                {
                                    ["name"] = call.Name,
                                    ["arguments"] = call.Input.ToJsonString()
                                }
                            });
                        }
                        entry["tool_calls"] = calls;
                    }
                    messages.Add(entry);
                    break;

                case ToolResultMessage result:
                    messages.Add(new JsonObject
                    {
                        ["role"] = "tool",
                        ["tool_call_id"] = result.CallId,
                        ["content"] = result.IsError ? "ERROR: " + result.Content : result.Content
                    });
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported message type {message.GetType().Name}.");
            }
        }

        var body = new JsonObject
        {
            ["model"] = _model,
            ["messages"] = messages,
            ["max_tokens"] = _maxTokens
        };

        if (tools.Count > 0)
        {
            var toolArray = new JsonArray();
            foreach (var tool in tools)
            {
                toolArray.Add(new JsonObject
                {
                    ["type"] = "function",
                    ["function"] = new JsonObject
                    {
                        ["name"] = tool.Name,
                        ["description"] = tool.Description,
                        ["parameters"] = tool.InputSchema.DeepClone()
                    }
                });
            }
            body["tools"] = toolArray;
        }

        var headers = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(_apiKey))
            headers["Authorization"] = "Bearer " + _apiKey;

        var response = await HttpRetry.PostJsonAsync(_http, _url, body, headers, ct);

        var reply = response["choices"]?[0]?["message"]
                    ?? throw new InvalidOperationException("Unexpected LLM response: no choices returned.");

        var replyText = reply["content"]?.GetValue<string>() ?? "";
        var toolCalls = new List<ToolCall>();

        if (reply["tool_calls"] is JsonArray rawCalls)
        {
            foreach (var raw in rawCalls)
            {
                var function = raw?["function"];
                if (function is null) continue;

                var id = raw?["id"]?.GetValue<string>();
                if (string.IsNullOrEmpty(id)) id = "call_" + Guid.NewGuid().ToString("N");

                toolCalls.Add(new ToolCall(id, function["name"]!.GetValue<string>(), ParseArguments(function["arguments"])));
            }
        }

        return new LlmReply(
            replyText,
            toolCalls,
            response["usage"]?["prompt_tokens"]?.GetValue<int>() ?? 0,
            response["usage"]?["completion_tokens"]?.GetValue<int>() ?? 0);
    }

    // Most servers send arguments as a JSON *string*; some send an object. Handle both,
    // and never crash on malformed arguments (small local models sometimes produce them).
    private static JsonNode ParseArguments(JsonNode? node)
    {
        if (node is null) return new JsonObject();

        if (node is JsonValue value && value.TryGetValue<string>(out var json))
        {
            try
            {
                return JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json) ?? new JsonObject();
            }
            catch (JsonException)
            {
                return new JsonObject();
            }
        }

        return node.DeepClone();
    }
}

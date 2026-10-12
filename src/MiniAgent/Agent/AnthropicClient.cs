using System.Text.Json.Nodes;

namespace MiniAgent.Agent;

/// <summary>Client for the Anthropic Messages API (optional - needs a paid API key).</summary>
public sealed class AnthropicClient : ILlmClient
{
    private readonly HttpClient _http;
    private readonly string _url;
    private readonly string _model;
    private readonly string _apiKey;
    private readonly int _maxTokens;

    public AnthropicClient(HttpClient http, string baseUrl, string model, string apiKey, int maxTokens = 1024)
    {
        _http = http;
        _url = baseUrl.TrimEnd('/') + "/v1/messages";
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
        var messages = new JsonArray();
        JsonArray? pendingResults = null;

        // Anthropic wants all tool results for one assistant turn in a single user message.
        void FlushResults()
        {
            if (pendingResults is null) return;
            messages.Add(new JsonObject { ["role"] = "user", ["content"] = pendingResults });
            pendingResults = null;
        }

        foreach (var message in history)
        {
            switch (message)
            {
                case ToolResultMessage result:
                    pendingResults ??= new JsonArray();
                    var block = new JsonObject
                    {
                        ["type"] = "tool_result",
                        ["tool_use_id"] = result.CallId,
                        ["content"] = result.Content
                    };
                    if (result.IsError) block["is_error"] = true;
                    pendingResults.Add(block);
                    break;

                case UserMessage user:
                    FlushResults();
                    messages.Add(new JsonObject { ["role"] = "user", ["content"] = user.Text });
                    break;

                case AssistantMessage assistant:
                    FlushResults();
                    var content = new JsonArray();
                    if (assistant.Text.Length > 0)
                        content.Add(new JsonObject { ["type"] = "text", ["text"] = assistant.Text });
                    foreach (var call in assistant.ToolCalls)
                    {
                        content.Add(new JsonObject
                        {
                            ["type"] = "tool_use",
                            ["id"] = call.Id,
                            ["name"] = call.Name,
                            ["input"] = call.Input.DeepClone()
                        });
                    }
                    messages.Add(new JsonObject { ["role"] = "assistant", ["content"] = content });
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported message type {message.GetType().Name}.");
            }
        }
        FlushResults();

        var toolArray = new JsonArray();
        foreach (var tool in tools)
        {
            toolArray.Add(new JsonObject
            {
                ["name"] = tool.Name,
                ["description"] = tool.Description,
                ["input_schema"] = tool.InputSchema.DeepClone()
            });
        }

        var body = new JsonObject
        {
            ["model"] = _model,
            ["max_tokens"] = _maxTokens,
            ["system"] = system,
            ["tools"] = toolArray,
            ["messages"] = messages
        };

        var headers = new Dictionary<string, string>
        {
            ["x-api-key"] = _apiKey,
            ["anthropic-version"] = "2023-06-01"
        };

        var response = await HttpRetry.PostJsonAsync(_http, _url, body, headers, ct);

        var texts = new List<string>();
        var toolCalls = new List<ToolCall>();
        foreach (var item in response["content"]?.AsArray() ?? new JsonArray())
        {
            switch (item?["type"]?.GetValue<string>())
            {
                case "text":
                    texts.Add(item["text"]?.GetValue<string>() ?? "");
                    break;
                case "tool_use":
                    toolCalls.Add(new ToolCall(
                        item["id"]!.GetValue<string>(),
                        item["name"]!.GetValue<string>(),
                        item["input"]?.DeepClone() ?? new JsonObject()));
                    break;
            }
        }

        return new LlmReply(
            string.Join("\n", texts),
            toolCalls,
            response["usage"]?["input_tokens"]?.GetValue<int>() ?? 0,
            response["usage"]?["output_tokens"]?.GetValue<int>() ?? 0);
    }
}

using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using MiniAgent.Agent;

namespace MiniAgent.Tests;

/// <summary>Verifies the wire-format translation of each provider using a stubbed HTTP handler.</summary>
public class LlmClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _response;
        public StubHandler(string response) => _response = response;
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_response, Encoding.UTF8, "application/json")
            };
        }
    }

    private static readonly ToolSpec[] Specs =
    {
        new("calculator", "math", new JsonObject { ["type"] = "object" })
    };

    private static ChatMessage[] HistoryWithTwoToolCalls() => new ChatMessage[]
    {
        new UserMessage("hi"),
        new AssistantMessage("", new[]
        {
            new ToolCall("c1", "calculator", new JsonObject { ["expression"] = "2+2" }),
            new ToolCall("c2", "calculator", new JsonObject { ["expression"] = "3+3" })
        }),
        new ToolResultMessage("c1", "calculator", "4", false),
        new ToolResultMessage("c2", "calculator", "6", false)
    };

    [Fact]
    public async Task OpenAi_client_parses_tool_calls_and_usage()
    {
        var handler = new StubHandler("""
            {
              "choices": [ { "message": { "role": "assistant", "content": null,
                "tool_calls": [ { "id": "call_1", "type": "function",
                  "function": { "name": "calculator", "arguments": "{\"expression\":\"2+2\"}" } } ] } } ],
              "usage": { "prompt_tokens": 12, "completion_tokens": 7 }
            }
            """);
        var client = new OpenAiCompatibleClient(new HttpClient(handler), "http://localhost:11434/v1", "test-model", null);

        var reply = await client.SendAsync("system prompt", Specs, new ChatMessage[] { new UserMessage("2+2?") });

        var call = Assert.Single(reply.ToolCalls);
        Assert.Equal("calculator", call.Name);
        Assert.Equal("call_1", call.Id);
        Assert.Equal("2+2", call.Input["expression"]!.GetValue<string>());
        Assert.Equal(12, reply.InputTokens);
        Assert.Equal(7, reply.OutputTokens);
    }

    [Fact]
    public async Task OpenAi_client_survives_malformed_tool_arguments()
    {
        var handler = new StubHandler("""
            { "choices": [ { "message": { "content": "", "tool_calls": [
              { "function": { "name": "calculator", "arguments": "{not json" } } ] } } ] }
            """);
        var client = new OpenAiCompatibleClient(new HttpClient(handler), "http://x/v1", "m", null);

        var reply = await client.SendAsync("s", Specs, new ChatMessage[] { new UserMessage("q") });

        var call = Assert.Single(reply.ToolCalls);
        Assert.False(string.IsNullOrEmpty(call.Id));      // id generated when the server omits it
        Assert.Empty(call.Input.AsObject());              // bad arguments become an empty object
    }

    [Fact]
    public async Task OpenAi_client_sends_each_tool_result_as_its_own_tool_message()
    {
        var handler = new StubHandler("""{ "choices": [ { "message": { "content": "ok" } } ] }""");
        var client = new OpenAiCompatibleClient(new HttpClient(handler), "http://x/v1", "m", null);

        await client.SendAsync("sys", Specs, HistoryWithTwoToolCalls());

        var messages = JsonNode.Parse(handler.LastBody!)!["messages"]!.AsArray();
        Assert.Equal(5, messages.Count); // system, user, assistant, tool, tool
        Assert.Equal("system", messages[0]!["role"]!.GetValue<string>());
        Assert.Equal("tool", messages[3]!["role"]!.GetValue<string>());
        Assert.Equal("c2", messages[4]!["tool_call_id"]!.GetValue<string>());
    }

    [Fact]
    public async Task Anthropic_client_merges_tool_results_into_one_user_message()
    {
        var handler = new StubHandler("""
            { "stop_reason": "end_turn", "content": [ { "type": "text", "text": "hello" } ],
              "usage": { "input_tokens": 1, "output_tokens": 2 } }
            """);
        var client = new AnthropicClient(new HttpClient(handler), "https://api.anthropic.com", "m", "key");

        var reply = await client.SendAsync("sys", Specs, HistoryWithTwoToolCalls());

        var messages = JsonNode.Parse(handler.LastBody!)!["messages"]!.AsArray();
        Assert.Equal(3, messages.Count); // user, assistant, user(2 tool_result blocks)
        Assert.Equal(2, messages[2]!["content"]!.AsArray().Count);
        Assert.Equal("hello", reply.Text);
        Assert.Equal(1, reply.InputTokens);
    }
}

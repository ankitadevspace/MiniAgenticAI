using System.Text.Json.Nodes;
using MiniAgent.Agent;
using MiniAgent.Tools;

namespace MiniAgent.Tests;

/// <summary>Fake LLM that replays scripted replies (repeating the last one) - no network needed.</summary>
internal sealed class ScriptedLlm : ILlmClient
{
    private readonly Queue<LlmReply> _replies;

    public ScriptedLlm(params LlmReply[] replies) => _replies = new Queue<LlmReply>(replies);

    public int Calls { get; private set; }
    public List<ChatMessage> LastHistory { get; private set; } = new();

    public Task<LlmReply> SendAsync(
        string system,
        IReadOnlyList<ToolSpec> tools,
        IReadOnlyList<ChatMessage> history,
        CancellationToken ct = default)
    {
        Calls++;
        LastHistory = history.ToList();
        var reply = _replies.Count > 1 ? _replies.Dequeue() : _replies.Peek();
        return Task.FromResult(reply);
    }
}

internal static class Replies
{
    public static LlmReply CallTool(string name, JsonObject input) =>
        new("Let me check.", new[] { new ToolCall("c1", name, input) }, 10, 5);

    public static LlmReply Final(string text) =>
        new(text, Array.Empty<ToolCall>(), 20, 8);
}

internal sealed class FakeApproval : IApprovalHandler
{
    private readonly bool _answer;
    public FakeApproval(bool answer) => _answer = answer;
    public bool Approve(string toolName, string inputJson) => _answer;
}

internal sealed class SpyTool : ITool
{
    public SpyTool(string name, bool requiresApproval)
    {
        Name = name;
        RequiresApproval = requiresApproval;
    }

    public string Name { get; }
    public string Description => "spy";
    public JsonObject InputSchema => new JsonObject { ["type"] = "object" };
    public bool RequiresApproval { get; }
    public bool Executed { get; private set; }

    public Task<string> ExecuteAsync(JsonNode input, CancellationToken ct)
    {
        Executed = true;
        return Task.FromResult("done");
    }
}

internal sealed class ThrowingTool : ITool
{
    public string Name => "explode";
    public string Description => "always fails";
    public JsonObject InputSchema => new JsonObject { ["type"] = "object" };
    public bool RequiresApproval => false;

    public Task<string> ExecuteAsync(JsonNode input, CancellationToken ct) =>
        throw new InvalidOperationException("boom");
}

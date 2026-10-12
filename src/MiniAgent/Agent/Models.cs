using System.Text.Json.Nodes;

namespace MiniAgent.Agent;

// Provider-neutral types. Each LLM client translates these to its own wire format.

public sealed record ToolCall(string Id, string Name, JsonNode Input);

public abstract record ChatMessage;
public sealed record UserMessage(string Text) : ChatMessage;
public sealed record AssistantMessage(string Text, IReadOnlyList<ToolCall> ToolCalls) : ChatMessage;
public sealed record ToolResultMessage(string CallId, string ToolName, string Content, bool IsError) : ChatMessage;

public sealed record LlmReply(string Text, IReadOnlyList<ToolCall> ToolCalls, int InputTokens, int OutputTokens);

public sealed record ToolSpec(string Name, string Description, JsonObject InputSchema);

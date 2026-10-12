namespace MiniAgent.Agent;

/// <summary>
/// Abstraction over the LLM provider. Lets the agent loop run against Ollama, any
/// OpenAI-compatible API, or Anthropic, and be unit-tested with a scripted fake.
/// </summary>
public interface ILlmClient
{
    Task<LlmReply> SendAsync(
        string system,
        IReadOnlyList<ToolSpec> tools,
        IReadOnlyList<ChatMessage> history,
        CancellationToken ct = default);
}

namespace MiniAgent.Agent;

/// <summary>
/// Which LLM to use, read from environment variables. Default is Ollama: free, local, no key.
/// </summary>
public sealed record LlmSettings(string Provider, string Model, string BaseUrl, string? ApiKey)
{
    public static LlmSettings FromEnvironment()
    {
        var provider = (Env("LLM_PROVIDER") ?? "ollama").ToLowerInvariant();

        switch (provider)
        {
            case "ollama":
                return new LlmSettings(
                    provider,
                    Env("LLM_MODEL") ?? "llama3.1",
                    Env("LLM_BASE_URL") ?? "http://localhost:11434/v1",
                    null);

            case "openai": // any OpenAI-compatible API: Groq, OpenRouter, Gemini, OpenAI...
                var openAiKey = Env("LLM_API_KEY")
                    ?? throw new InvalidOperationException("LLM_API_KEY is required when LLM_PROVIDER=openai.");
                var openAiModel = Env("LLM_MODEL")
                    ?? throw new InvalidOperationException("LLM_MODEL is required when LLM_PROVIDER=openai.");
                return new LlmSettings(
                    provider,
                    openAiModel,
                    Env("LLM_BASE_URL") ?? "https://api.openai.com/v1",
                    openAiKey);

            case "anthropic":
                var anthropicKey = Env("LLM_API_KEY") ?? Env("ANTHROPIC_API_KEY")
                    ?? throw new InvalidOperationException("Set ANTHROPIC_API_KEY (or LLM_API_KEY) when LLM_PROVIDER=anthropic.");
                return new LlmSettings(
                    provider,
                    Env("LLM_MODEL") ?? "claude-sonnet-5-5",
                    Env("LLM_BASE_URL") ?? "https://api.anthropic.com",
                    anthropicKey);

            default:
                throw new InvalidOperationException(
                    $"Unknown LLM_PROVIDER '{provider}'. Use ollama, openai or anthropic.");
        }
    }

    private static string? Env(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}

public static class LlmFactory
{
    public static ILlmClient Create(LlmSettings settings, HttpClient http) =>
        settings.Provider == "anthropic"
            ? new AnthropicClient(http, settings.BaseUrl, settings.Model, settings.ApiKey!)
            : new OpenAiCompatibleClient(http, settings.BaseUrl, settings.Model, settings.ApiKey);
}

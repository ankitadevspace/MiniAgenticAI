using System.Text.Json.Nodes;

namespace MiniAgent.Tools;

public interface ITool
{
    string Name { get; }
    string Description { get; }

    /// <summary>JSON Schema describing the tool's arguments.</summary>
    JsonObject InputSchema { get; }

    /// <summary>If true, the human must approve each call before it runs.</summary>
    bool RequiresApproval { get; }

    Task<string> ExecuteAsync(JsonNode input, CancellationToken ct);
}

public static class ToolInput
{
    public static string RequireString(JsonNode input, string key)
    {
        var value = input[key]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"Missing required argument '{key}'.");
        return value;
    }
}

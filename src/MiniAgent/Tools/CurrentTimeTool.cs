using System.Globalization;
using System.Text.Json.Nodes;

namespace MiniAgent.Tools;

public sealed class CurrentTimeTool : ITool
{
    public string Name => "get_current_time";

    public string Description => "Get the current local date and time.";

    public JsonObject InputSchema => JsonNode.Parse("""
        { "type": "object", "properties": {} }
        """)!.AsObject();

    public bool RequiresApproval => false;

    public Task<string> ExecuteAsync(JsonNode input, CancellationToken ct) =>
        Task.FromResult(DateTimeOffset.Now.ToString("dddd, dd MMMM yyyy HH:mm zzz", CultureInfo.InvariantCulture));
}

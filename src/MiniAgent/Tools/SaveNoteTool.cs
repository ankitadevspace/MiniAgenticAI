using System.Text.Json.Nodes;

namespace MiniAgent.Tools;

/// <summary>A side-effecting tool, so it is gated behind human approval.</summary>
public sealed class SaveNoteTool : ITool
{
    private readonly string _path;

    public SaveNoteTool(string path) => _path = path;

    public string Name => "save_note";

    public string Description => "Save a short note to a file. Only use when the user explicitly asks to save something.";

    public JsonObject InputSchema => JsonNode.Parse("""
        {
          "type": "object",
          "properties": { "text": { "type": "string", "description": "The note to save" } },
          "required": ["text"]
        }
        """)!.AsObject();

    public bool RequiresApproval => true;

    public async Task<string> ExecuteAsync(JsonNode input, CancellationToken ct)
    {
        var text = ToolInput.RequireString(input, "text");

        var directory = Path.GetDirectoryName(Path.GetFullPath(_path));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        await File.AppendAllTextAsync(_path, $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm}] {text}{Environment.NewLine}", ct);
        return $"Note saved to {_path}.";
    }
}

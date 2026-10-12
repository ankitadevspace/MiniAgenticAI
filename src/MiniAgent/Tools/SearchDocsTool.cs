using System.Text.Json.Nodes;
using MiniAgent.Rag;

namespace MiniAgent.Tools;

/// <summary>RAG as a tool: the agent decides when to look things up in your documents.</summary>
public sealed class SearchDocsTool : ITool
{
    private readonly KnowledgeBase _knowledge;
    private readonly int _topK;

    public SearchDocsTool(KnowledgeBase knowledge, int topK = 3)
    {
        _knowledge = knowledge;
        _topK = topK;
    }

    public string Name => "search_docs";

    public string Description =>
        "Search the company documents for policies, pricing, shipping and support information. " +
        "Returns the most relevant passages, each prefixed with its source in square brackets.";

    public JsonObject InputSchema => JsonNode.Parse("""
        {
          "type": "object",
          "properties": { "query": { "type": "string", "description": "Keywords to search for" } },
          "required": ["query"]
        }
        """)!.AsObject();

    public bool RequiresApproval => false;

    public Task<string> ExecuteAsync(JsonNode input, CancellationToken ct)
    {
        var query = ToolInput.RequireString(input, "query");
        var results = _knowledge.Search(query, _topK);

        if (results.Count == 0)
            return Task.FromResult("No relevant information found in the documents.");

        var text = string.Join("\n\n",
            results.Select(r => $"[{r.Chunk.Source}#{r.Chunk.Index}] {r.Chunk.Text}"));
        return Task.FromResult(text);
    }
}

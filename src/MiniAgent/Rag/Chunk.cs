namespace MiniAgent.Rag;

public sealed record Chunk(string Source, int Index, string Text);

public sealed record SearchResult(Chunk Chunk, double Score);

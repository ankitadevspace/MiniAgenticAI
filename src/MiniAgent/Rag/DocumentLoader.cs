namespace MiniAgent.Rag;

public static class DocumentLoader
{
    private static readonly string[] Extensions = { ".txt", ".md" };

    public static List<Chunk> LoadDirectory(string path, int maxChars = 500, int overlap = 80)
    {
        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException($"Docs folder not found: {Path.GetFullPath(path)}");

        var files = Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
            .Where(f => Extensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.Ordinal);

        var chunks = new List<Chunk>();
        foreach (var file in files)
        {
            var name = Path.GetRelativePath(path, file).Replace('\\', '/');
            chunks.AddRange(Chunker.Split(name, File.ReadAllText(file), maxChars, overlap));
        }
        return chunks;
    }
}

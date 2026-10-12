using System.Text;

namespace MiniAgent.Rag;

/// <summary>
/// Splits a document into overlapping chunks. Paragraphs are kept together where possible;
/// paragraphs longer than maxChars are cut at word boundaries.
/// </summary>
public static class Chunker
{
    public static List<Chunk> Split(string source, string text, int maxChars = 500, int overlap = 80)
    {
        var pieces = new List<string>();
        var paragraphs = text.Replace("\r\n", "\n")
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var paragraph in paragraphs)
        {
            if (paragraph.Length <= maxChars) pieces.Add(paragraph);
            else pieces.AddRange(HardSplit(paragraph, maxChars));
        }

        var chunks = new List<Chunk>();
        var current = new StringBuilder();
        var hasNewContent = false; // false when 'current' only holds overlap carried from the previous chunk

        void Flush()
        {
            hasNewContent = false;
            var chunkText = current.ToString().Trim();
            if (chunkText.Length == 0) return;

            chunks.Add(new Chunk(source, chunks.Count, chunkText));

            // Seed the next chunk with the tail of this one so context isn't lost at boundaries.
            current.Clear();
            if (overlap > 0 && chunkText.Length > overlap)
            {
                var tail = chunkText[^overlap..];
                var firstSpace = tail.IndexOf(' ');
                if (firstSpace >= 0) tail = tail[(firstSpace + 1)..];
                current.Append(tail);
            }
        }

        foreach (var piece in pieces)
        {
            if (current.Length > 0 && current.Length + piece.Length + 1 > maxChars)
                Flush();

            if (current.Length > 0) current.Append('\n');
            current.Append(piece);
            hasNewContent = true;
        }

        if (hasNewContent)
        {
            var last = current.ToString().Trim();
            if (last.Length > 0) chunks.Add(new Chunk(source, chunks.Count, last));
        }

        return chunks;
    }

    private static IEnumerable<string> HardSplit(string text, int max)
    {
        var start = 0;
        while (start < text.Length)
        {
            var length = Math.Min(max, text.Length - start);
            if (start + length < text.Length)
            {
                var lastSpace = text.LastIndexOf(' ', start + length - 1, length);
                if (lastSpace > start) length = lastSpace - start;
            }

            var piece = text.Substring(start, length).Trim();
            if (piece.Length > 0) yield return piece;
            start += length;
        }
    }
}

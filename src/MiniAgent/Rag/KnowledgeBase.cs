using System.Text.RegularExpressions;

namespace MiniAgent.Rag;

/// <summary>
/// In-memory keyword search index using BM25 ranking (the same family of algorithm that
/// powers Elasticsearch/Lucene). Runs fully offline - no embeddings API, no cost.
/// To upgrade to semantic search, replace this class with embeddings + a vector store.
/// </summary>
public sealed class KnowledgeBase
{
    private const double K1 = 1.5;
    private const double B = 0.75;

    private static readonly Regex WordPattern = new("[a-z0-9]+", RegexOptions.Compiled);

    private static readonly HashSet<string> StopWords = new()
    {
        "a", "an", "the", "is", "are", "was", "were", "be", "of", "to", "in", "on", "for", "and", "or",
        "what", "how", "do", "does", "can", "i", "my", "me", "you", "your", "it", "with", "at", "by",
        "from", "that", "this", "as", "we", "our", "if", "will", "about", "tell"
    };

    private readonly List<Chunk> _chunks = new();
    private readonly List<Dictionary<string, int>> _termFrequencies = new();
    private readonly List<int> _lengths = new();
    private readonly Dictionary<string, int> _documentFrequency = new();
    private double _averageLength;

    public int ChunkCount => _chunks.Count;

    public void Add(IEnumerable<Chunk> chunks)
    {
        foreach (var chunk in chunks)
        {
            var tokens = Tokenize(chunk.Text);
            var frequencies = new Dictionary<string, int>();
            foreach (var token in tokens)
                frequencies[token] = frequencies.GetValueOrDefault(token) + 1;

            foreach (var term in frequencies.Keys)
                _documentFrequency[term] = _documentFrequency.GetValueOrDefault(term) + 1;

            _chunks.Add(chunk);
            _termFrequencies.Add(frequencies);
            _lengths.Add(tokens.Count);
        }

        _averageLength = _lengths.Count == 0 ? 0 : _lengths.Average();
    }

    public IReadOnlyList<SearchResult> Search(string query, int topK = 3)
    {
        var queryTerms = Tokenize(query).Distinct().ToList();
        if (queryTerms.Count == 0 || _chunks.Count == 0)
            return Array.Empty<SearchResult>();

        var total = _chunks.Count;
        var results = new List<SearchResult>();

        for (var i = 0; i < total; i++)
        {
            var lengthRatio = _averageLength == 0 ? 1 : _lengths[i] / _averageLength;
            double score = 0;

            foreach (var term in queryTerms)
            {
                if (!_termFrequencies[i].TryGetValue(term, out var tf)) continue;

                var df = _documentFrequency[term];
                var idf = Math.Log(1 + (total - df + 0.5) / (df + 0.5));
                score += idf * (tf * (K1 + 1)) / (tf + K1 * (1 - B + B * lengthRatio));
            }

            if (score > 0) results.Add(new SearchResult(_chunks[i], score));
        }

        return results.OrderByDescending(r => r.Score).Take(topK).ToList();
    }

    private static List<string> Tokenize(string text) =>
        WordPattern.Matches(text.ToLowerInvariant())
            .Select(m => m.Value)
            .Where(w => !StopWords.Contains(w))
            .Select(Stem)
            .ToList();

    // Tiny stemmer so "refunds" matches "refund".
    private static string Stem(string word) =>
        word.Length > 3 && word.EndsWith('s') && !word.EndsWith("ss") ? word[..^1] : word;
}

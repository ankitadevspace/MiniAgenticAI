using MiniAgent.Rag;

namespace MiniAgent.Tests;

public class ChunkerTests
{
    [Fact]
    public void Empty_text_produces_no_chunks()
    {
        Assert.Empty(Chunker.Split("a.md", "   \n\n  "));
    }

    [Fact]
    public void Short_text_is_a_single_chunk()
    {
        var chunk = Assert.Single(Chunker.Split("a.md", "Hello world."));

        Assert.Equal("Hello world.", chunk.Text);
        Assert.Equal("a.md", chunk.Source);
        Assert.Equal(0, chunk.Index);
    }

    [Fact]
    public void Long_text_is_split_with_sequential_indexes()
    {
        var text = string.Join("\n\n",
            Enumerable.Range(1, 20).Select(i => $"Paragraph number {i} has some filler words to take up space."));

        var chunks = Chunker.Split("a.md", text, maxChars: 200, overlap: 20);

        Assert.True(chunks.Count > 1);
        Assert.Equal(Enumerable.Range(0, chunks.Count), chunks.Select(c => c.Index));
    }

    [Fact]
    public void Oversized_paragraph_is_split_at_the_size_limit()
    {
        var text = string.Join(' ', Enumerable.Repeat("word", 300));

        var chunks = Chunker.Split("a.md", text, maxChars: 100, overlap: 0);

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, c => Assert.True(c.Text.Length <= 100));
    }

    [Fact]
    public void No_text_is_lost_when_splitting_without_overlap()
    {
        var words = Enumerable.Range(1, 200).Select(i => $"w{i}").ToList();
        var text = string.Join(' ', words);

        var chunks = Chunker.Split("a.md", text, maxChars: 80, overlap: 0);
        var rebuilt = string.Join(' ', chunks.Select(c => c.Text)).Split(new[] { ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(words, rebuilt);
    }
}

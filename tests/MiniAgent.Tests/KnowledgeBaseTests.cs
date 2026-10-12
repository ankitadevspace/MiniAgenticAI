using MiniAgent.Rag;

namespace MiniAgent.Tests;

public class KnowledgeBaseTests
{
    private static KnowledgeBase Build()
    {
        var knowledge = new KnowledgeBase();
        knowledge.Add(new[]
        {
            new Chunk("refund.md", 0, "Customers can request a refund within 30 days of purchase."),
            new Chunk("shipping.md", 0, "Standard delivery takes 5 to 7 business days."),
            new Chunk("support.md", 0, "Support hours are Monday to Friday, 9am to 6pm IST.")
        });
        return knowledge;
    }

    [Fact]
    public void Finds_the_most_relevant_chunk()
    {
        var results = Build().Search("what is the refund policy");

        Assert.Equal("refund.md", results[0].Chunk.Source);
    }

    [Fact]
    public void Plural_query_matches_singular_text()
    {
        var results = Build().Search("refunds");

        Assert.Equal("refund.md", Assert.Single(results).Chunk.Source);
    }

    [Fact]
    public void Unrelated_query_returns_nothing()
    {
        Assert.Empty(Build().Search("quantum physics"));
    }

    [Fact]
    public void Empty_index_returns_nothing()
    {
        Assert.Empty(new KnowledgeBase().Search("refund"));
    }

    [Fact]
    public void Respects_topK()
    {
        var results = Build().Search("days", topK: 1);

        Assert.Single(results);
    }
}

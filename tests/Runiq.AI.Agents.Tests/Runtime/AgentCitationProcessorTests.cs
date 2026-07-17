using Runiq.AI.Agents.Runtime;
using Runiq.AI.Rag.Models.Documents;
using Runiq.AI.Rag.Models.Search;

namespace Runiq.AI.Agents.Tests.Runtime;

public sealed class AgentCitationProcessorTests
{
    // Verifies normal, adjacent, and duplicate citation markers are parsed deterministically.
    [Fact]
    public void Parse_ShouldRecognizeSupportedMarkers()
    {
        var result = AgentCitationProcessor.Parse("First [1], both [1][2], again [1].");

        Assert.Equal([1, 1, 2, 1], result);
    }

    // Verifies invalid labels, zero, overflow, links, code, and escaped literals are not citations.
    [Fact]
    public void Parse_ShouldIgnoreInvalidAndMarkdownProtectedMarkers()
    {
        var result = AgentCitationProcessor.Parse("[0] [abc] [999999999999] [1](url) `code [2]` \\[3]\n```text\n[4]\n```");

        Assert.Empty(result);
    }

    // Verifies nested brackets, links, images, Unicode digits, signs, and whitespace never become citations.
    [Theory]
    [InlineData("[[1]]")]
    [InlineData("[[[1]]]")]
    [InlineData("[ 1 ]")]
    [InlineData("[-1]")]
    [InlineData("[+1]")]
    [InlineData("[١]")]
    [InlineData("![1](image.png)")]
    [InlineData("[label [1]](url)")]
    [InlineData("[label](https://example.test/[1])")]
    public void Parse_ShouldRejectProtectedOrMalformedBracketForms(string response)
    {
        Assert.Empty(AgentCitationProcessor.Parse(response));
    }

    // Verifies matching backtick runs of any length protect citation-looking code content.
    [Theory]
    [InlineData("`code [1]`")]
    [InlineData("``code [1]``")]
    [InlineData("````code [1]````")]
    [InlineData("````\ncode [1]\n````")]
    public void Parse_ShouldRejectMarkersInsideMatchingBacktickRuns(string response)
    {
        Assert.Empty(AgentCitationProcessor.Parse(response));
    }

    // Verifies only referenced selected sources become metadata and duplicate markers share one source entry.
    [Fact]
    public void Validate_ShouldMapSelectedSourceAndCountOccurrences()
    {
        var context = CreateContext();

        var citation = Assert.Single(AgentCitationProcessor.Validate("Answer [1], repeated [1], invalid [9].", context));

        Assert.Equal(1, citation.Number);
        Assert.Equal("document-1", citation.DocumentId);
        Assert.Equal("chunk-1", citation.ChunkId);
        Assert.Equal("retrieval-1", citation.RetrievalCorrelationId);
        Assert.Equal(2, citation.MarkerCount);
    }

    // Verifies no-context responses cannot produce validated citation metadata.
    [Fact]
    public void Validate_ShouldRejectCitationWithoutSelectedContext()
    {
        Assert.Empty(AgentCitationProcessor.Validate("Unsupported [1].", new AgentRuntimeContext()));
    }

    // Verifies AgentCitation rejects invalid numbering, identifiers, scores, relevance, and metric combinations.
    [Fact]
    public void AgentCitation_ShouldEnforcePublicInvariants()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateCitation(number: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateCitation(contextOrder: -1));
        Assert.Throws<ArgumentException>(() => CreateCitation(number: 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateCitation(markerCount: 0));
        Assert.Throws<ArgumentException>(() => CreateCitation(documentId: " "));
        Assert.Throws<ArgumentException>(() => CreateCitation(chunkId: " "));
        Assert.Throws<ArgumentException>(() => CreateCitation(correlationId: " "));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateCitation(rawScore: double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateCitation(rawScore: double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateCitation(relevance: -0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateCitation(relevance: 1.1));
        Assert.Throws<ArgumentException>(() => CreateCitation(metric: null, higherIsBetter: true));
        Assert.Throws<ArgumentException>(() => CreateCitation(metric: " ", higherIsBetter: null));
    }

    // Verifies terminal events and results retain citation snapshots after caller collection mutation.
    [Fact]
    public void CitationCollections_ShouldDefensivelyCopyCallerLists()
    {
        var source = new List<AgentCitation> { CreateCitation() };
        var executionEvent = AgentExecutionEvent.Completed(rag: null, source);
        var result = AgentExecutionResult.Success("answer", [], rag: null, source);

        source.Clear();

        Assert.Single(executionEvent.Citations);
        Assert.Single(result.Citations);
    }

    private static AgentRuntimeContext CreateContext() => new(
        [new RagSearchResult
        {
            Chunk = new RagChunk { Id = "chunk-1", DocumentId = "document-1", Content = "content" },
            RawScore = 0.9,
            Relevance = 0.9,
            Metric = "cosine_similarity",
            HigherIsBetter = true,
        }],
        [],
        [],
        null)
    {
        RetrievalCorrelationId = "retrieval-1",
    };

    private static AgentCitation CreateCitation(
        int number = 1, int contextOrder = 0, int markerCount = 1,
        string documentId = "document-1", string chunkId = "chunk-1", string correlationId = "retrieval-1",
        double? rawScore = 0.9, double? relevance = 0.9, string? metric = "cosine_similarity", bool? higherIsBetter = true) =>
        new(number, documentId, chunkId, correlationId, contextOrder, markerCount, rawScore, relevance, metric, higherIsBetter);
}

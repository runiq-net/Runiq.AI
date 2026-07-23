using System.Text.Json;
using System.Text.Json.Serialization;
using Runiq.AI.Rag.Models.Metadata;
using Runiq.AI.Rag.Models.Retrieval;

namespace Runiq.AI.Rag.Tests.Retrieval;

public sealed class RetrievalSerializationTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    // Verifies direct JSON preserves both valid semantic score directions together with their metric.
    public void SemanticResult_ShouldSerializeScoreDirection(bool higherIsBetter)
    {
        var item = new RetrievalResultItem
        {
            RecordId = "semantic",
            Content = "semantic content",
            RawScore = 0.8,
            Relevance = 0.9,
            Metric = "semantic-metric",
            HigherIsBetter = higherIsBetter,
            Provenance = new RagRetrievalProvenance
            {
                Mode = RagRetrievalMode.Semantic,
                SemanticRank = 1,
                SemanticRawScore = 0.8,
            },
        };

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(item, SerializerOptions));
        var root = json.RootElement;
        Assert.Equal(0.8, root.GetProperty("rawScore").GetDouble());
        Assert.Equal(0.9, root.GetProperty("relevance").GetDouble());
        Assert.Equal("semantic-metric", root.GetProperty("metric").GetString());
        Assert.Equal(higherIsBetter, root.GetProperty("higherIsBetter").GetBoolean());
        Assert.Equal(1, root.GetProperty("provenance").GetProperty("semanticRank").GetInt32());
    }

    [Fact]
    // Verifies lexical-only direct JSON omits semantic fields while retaining lexical provenance.
    public void LexicalResult_ShouldOmitSemanticFieldsAndSerializeLexicalProvenance()
    {
        var item = new RetrievalResultItem
        {
            RecordId = "lexical",
            Content = "lexical content",
            RawScore = null,
            Relevance = null,
            Metric = null,
            HigherIsBetter = null,
            Provenance = new RagRetrievalProvenance
            {
                Mode = RagRetrievalMode.Lexical,
                LexicalRank = 1,
                LexicalRawScore = 1.25,
            },
        };

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(item, SerializerOptions));
        var root = json.RootElement;
        Assert.False(root.TryGetProperty("rawScore", out _));
        Assert.False(root.TryGetProperty("relevance", out _));
        Assert.False(root.TryGetProperty("metric", out _));
        Assert.False(root.TryGetProperty("higherIsBetter", out _));
        Assert.Equal(1.25, root.GetProperty("provenance").GetProperty("lexicalRawScore").GetDouble());
    }

    [Fact]
    // Verifies direct statistics JSON distinguishes unavailable metadata from authoritative zero values.
    public void RetrievalStatistics_ShouldDistinguishUnknownFromKnownZero()
    {
        var unknownJson = JsonSerializer.Serialize(RagRetrievalStatistics.Empty, SerializerOptions);
        var knownJson = JsonSerializer.Serialize(new RagRetrievalStatistics
        {
            SemanticCandidateCount = 0,
            LexicalCandidateCount = 4,
            FusedCandidateCount = 0,
        }, SerializerOptions);

        using var unknown = JsonDocument.Parse(unknownJson);
        using var known = JsonDocument.Parse(knownJson);
        Assert.False(unknown.RootElement.TryGetProperty("semanticCandidateCount", out _));
        Assert.False(unknown.RootElement.TryGetProperty("lexicalCandidateCount", out _));
        Assert.False(unknown.RootElement.TryGetProperty("fusedCandidateCount", out _));
        Assert.Equal(0, known.RootElement.GetProperty("semanticCandidateCount").GetInt32());
        Assert.Equal(4, known.RootElement.GetProperty("lexicalCandidateCount").GetInt32());
        Assert.Equal(0, known.RootElement.GetProperty("fusedCandidateCount").GetInt32());
    }

    [Fact]
    // Verifies hybrid direct JSON keeps semantic, lexical, RRF, and fused-rank values in structured provenance.
    public void HybridResult_ShouldSerializeStructuredProvenance()
    {
        var item = new RetrievalResultItem
        {
            RecordId = "hybrid",
            Content = "hybrid content",
            Metadata = RagMetadata.Empty,
            RawScore = 0.7,
            Relevance = 0.8,
            Metric = "cosine-similarity",
            HigherIsBetter = true,
            Provenance = new RagRetrievalProvenance
            {
                Mode = RagRetrievalMode.Hybrid,
                SemanticRank = 2,
                LexicalRank = 1,
                SemanticRawScore = 0.7,
                LexicalRawScore = 1.4,
                ReciprocalRankFusionScore = 0.032,
                FusedRank = 1,
            },
        };

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(item, SerializerOptions));
        var provenance = json.RootElement.GetProperty("provenance");
        Assert.Equal("Hybrid", provenance.GetProperty("mode").GetString());
        Assert.Equal(2, provenance.GetProperty("semanticRank").GetInt32());
        Assert.Equal(1, provenance.GetProperty("lexicalRank").GetInt32());
        Assert.Equal(0.7, provenance.GetProperty("semanticRawScore").GetDouble());
        Assert.Equal(1.4, provenance.GetProperty("lexicalRawScore").GetDouble());
        Assert.Equal(0.032, provenance.GetProperty("reciprocalRankFusionScore").GetDouble());
        Assert.Equal(1, provenance.GetProperty("fusedRank").GetInt32());
    }
}

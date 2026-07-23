using Runiq.AI.Agents.Configuration;
using Runiq.AI.Agents.Runtime;
using Runiq.AI.Rag.Models.Documents;
using Runiq.AI.Rag.Models.Metadata;
using Runiq.AI.Rag.Models.Search;

namespace Runiq.AI.Agents.Tests.Runtime;

public sealed class RagContextAssemblerTests
{
    // Proves mandatory prompt components and the response reserve are deducted before selecting complete chunks.
    [Fact]
    public void Assemble_ShouldDeductMandatoryPromptAndResponseReserve()
    {
        var result = RagContextAssembler.Assemble(
            [Create("a", "doc-a", "one two three")],
            new RagContextBudgetOptions(),
            maximumContextTokens: 30,
            responseTokenReserve: 5,
            instructionsTokens: 4,
            conversationHistoryTokens: 3,
            userQueryTokens: 2,
            otherRequiredPromptTokens: 1);

        Assert.NotNull(result.Budget);
        Assert.Equal(15, result.Budget.AvailableRagContextTokens);
        Assert.True(result.Budget.SelectedRagContextTokens <= result.Budget.AvailableRagContextTokens);
    }

    // Proves a chunk that does not fit is skipped intact and remains observable as an accepted context exclusion.
    [Fact]
    public void Assemble_ShouldSkipCompleteChunkAndRecordTokenBudgetReason()
    {
        var result = RagContextAssembler.Assemble(
            [Create("large", "doc-a", string.Join(' ', Enumerable.Repeat("word", 40)))],
            new RagContextBudgetOptions(),
            maximumContextTokens: 20,
            responseTokenReserve: 5,
            instructionsTokens: 1,
            conversationHistoryTokens: 0,
            userQueryTokens: 1,
            otherRequiredPromptTokens: 1);

        Assert.Empty(result.SelectedResults);
        Assert.Equal(RagContextSelectionExclusionReason.TokenBudgetExceeded, Assert.Single(result.ExcludedResults).Reason);
    }

    // Proves stable document boundaries eliminate only materially overlapping chunks from the same document.
    [Fact]
    public void Assemble_ShouldReduceMetadataBackedOverlapWithinSameDocument()
    {
        var selected = Create("first", "doc-a", "alpha beta", 0, 100);
        var overlapping = Create("second", "doc-a", "gamma delta", 40, 120);
        var unrelatedSource = Create("third", "doc-b", "alpha beta", 40, 120);

        var result = AssembleWithRoom([selected, overlapping, unrelatedSource]);

        Assert.Equal(["first", "third"], result.SelectedResults.Select(item => item.Chunk.Id));
        Assert.Equal(RagContextSelectionExclusionReason.OverlappingContent, Assert.Single(result.ExcludedResults).Reason);
    }

    // Proves the bounded per-source policy prevents one document from monopolizing selected context.
    [Fact]
    public void Assemble_ShouldEnforceMaximumChunksPerSource()
    {
        var options = new RagContextBudgetOptions { MaximumChunksPerSource = 1 };
        var result = AssembleWithRoom(
            [Create("a1", "a", "one"), Create("a2", "a", "two"), Create("b1", "b", "three")],
            options);

        Assert.Equal(["a1", "b1"], result.SelectedResults.Select(item => item.Chunk.Id));
        Assert.Equal(RagContextSelectionExclusionReason.SourceLimitExceeded, Assert.Single(result.ExcludedResults).Reason);
    }

    // Proves diversity uses deterministic source rounds while retaining retrieval order inside each source.
    [Fact]
    public void Assemble_ShouldPreferDeterministicSourceDiversity()
    {
        var options = new RagContextBudgetOptions { PreferSourceDiversity = true };
        var result = AssembleWithRoom(
            [Create("a1", "a", "one"), Create("a2", "a", "two"), Create("b1", "b", "three"), Create("b2", "b", "four")],
            options);

        Assert.Equal(["a1", "b1", "a2", "b2"], result.SelectedResults.Select(item => item.Chunk.Id));
    }

    // Proves mandatory prompt overflow is structured and prevents any selected context.
    [Fact]
    public void Assemble_ShouldReportMandatoryPromptOverflow()
    {
        var result = RagContextAssembler.Assemble(
            [Create("a", "a", "one")],
            new RagContextBudgetOptions(),
            maximumContextTokens: 10,
            responseTokenReserve: 5,
            instructionsTokens: 3,
            conversationHistoryTokens: 2,
            userQueryTokens: 1,
            otherRequiredPromptTokens: 0);

        Assert.True(result.MandatoryPromptOverflow);
        Assert.NotNull(result.Budget);
        Assert.Equal(0, result.Budget.AvailableRagContextTokens);
        Assert.Empty(result.SelectedResults);
    }

    private static RagContextAssembly AssembleWithRoom(
        IReadOnlyList<RagSearchResult> results,
        RagContextBudgetOptions? options = null) =>
        RagContextAssembler.Assemble(results, options ?? new RagContextBudgetOptions(),
            10_000, 100, 10, 0, 10, 10);

    private static RagSearchResult Create(
        string chunkId,
        string documentId,
        string content,
        int? start = null,
        int? end = null) =>
        new()
        {
            Chunk = new RagChunk
            {
                Id = chunkId,
                DocumentId = documentId,
                Content = content,
                Metadata = new RagChunkMetadata { StartIndex = start, EndIndex = end },
            },
            RawScore = 0.9,
            Metric = "cosine_similarity",
            HigherIsBetter = true,
        };
}

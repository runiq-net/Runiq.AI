using Runiq.AI.Rag.Models.Retrieval;
using Runiq.AI.Rag.Tests.Retrieval.Integration.Support;

namespace Runiq.AI.Rag.Tests.Retrieval.Integration;

/// <summary>
/// End-to-end integration tests that drive the agent-facing <c>IVectorQueryTool</c> through the real RAG
/// dependency injection graph — deterministic keyword embedding, in-memory vector store, and the resolved
/// retrieval pipeline the tool delegates to. They prove that a tool query retrieves seeded in-memory vector
/// results honoring index name, TopK, and metadata filter, and that content, score, and metadata survive the
/// mapping into the tool result. No real embedding provider, network, database, or external SDK is involved,
/// and every scenario is deterministic.
/// </summary>
public sealed class VectorQueryToolIntegrationTests
{
    private const string PrimaryIndex = "platform-handbook";
    private const string SecondaryIndex = "culinary-handbook";
    private const string EmptyIndex = "empty-handbook";

    private const string DatabaseTuningContent = "Database index tuning improves query performance.";
    private const string DatabaseBackupContent = "Database backup restore schedule.";
    private const string NetworkLatencyContent = "Network latency security review.";
    private const string CookingContent = "Cooking recipe travel guide.";

    private const string DatabaseTuningId = "kb-database-tuning";
    private const string DatabaseBackupId = "kb-database-backup";
    private const string NetworkLatencyId = "kb-network-latency";
    private const string CookingId = "kb-cooking";

    private const string DatabaseTuningQuery = "database index tuning performance";

    [Fact]
    public async Task Execute_ShouldRetrieveClosestSeededChunkFromTargetIndex()
    {
        using var context = RetrievalIntegrationContext.Create();
        await SeedKnowledgeBaseAsync(context, PrimaryIndex);

        var result = await context.ExecuteVectorQueryToolAsync(PrimaryIndex, DatabaseTuningQuery);

        // Verifies the tool embeds the query text and returns the closest seeded chunk from the target index.
        Assert.True(result.Succeeded);
        Assert.Equal(RetrievalErrorCode.None, result.ErrorCode);
        Assert.NotEmpty(result.Matches);
        Assert.Equal(DatabaseTuningId, result.Matches[0].RecordId);
    }

    [Fact]
    public async Task Execute_ShouldOnlyReturnRecordsStoredUnderRequestedIndexName()
    {
        using var context = RetrievalIntegrationContext.Create();
        await SeedKnowledgeBaseAsync(context, PrimaryIndex);
        await context.SeedAsync(SecondaryIndex, new RetrievalSeedRecord(
            "kb-secondary-database",
            DatabaseTuningContent,
            new Dictionary<string, string> { ["category"] = "database" }));

        var result = await context.ExecuteVectorQueryToolAsync(PrimaryIndex, DatabaseTuningQuery);

        // Verifies the tool honors the index name and never returns records stored under a different index.
        Assert.True(result.Succeeded);
        Assert.NotEmpty(result.Matches);
        Assert.All(result.Matches, match => Assert.NotEqual("kb-secondary-database", match.RecordId));
        Assert.Contains(result.Matches, match => match.RecordId == DatabaseTuningId);
    }

    [Fact]
    public async Task Execute_ShouldLimitMatchCountToTopK()
    {
        using var context = RetrievalIntegrationContext.Create();
        await SeedKnowledgeBaseAsync(context, PrimaryIndex);

        var result = await context.ExecuteVectorQueryToolAsync(PrimaryIndex, DatabaseTuningQuery, topK: 2);

        // Verifies the tool forwards TopK so the match count is capped even though the index holds more records.
        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Matches.Count);
    }

    [Fact]
    public async Task Execute_ShouldOrderMatchesBySimilarityScoreDescending()
    {
        using var context = RetrievalIntegrationContext.Create();
        await SeedKnowledgeBaseAsync(context, PrimaryIndex);

        var result = await context.ExecuteVectorQueryToolAsync(PrimaryIndex, DatabaseTuningQuery, topK: 2);

        // Verifies the tool preserves the pipeline's descending similarity ordering and score values.
        Assert.Equal(2, result.Matches.Count);
        Assert.Equal(DatabaseTuningId, result.Matches[0].RecordId);
        Assert.Equal(DatabaseBackupId, result.Matches[1].RecordId);
        Assert.True(result.Matches[0].Score > result.Matches[1].Score);
    }

    [Fact]
    public async Task Execute_ShouldPreserveContentScoreAndMetadataOnMatches()
    {
        using var context = RetrievalIntegrationContext.Create();
        await SeedKnowledgeBaseAsync(context, PrimaryIndex);

        var result = await context.ExecuteVectorQueryToolAsync(PrimaryIndex, DatabaseTuningQuery);

        // Verifies the tool result carries the seeded chunk content, a positive score, and the stored metadata.
        Assert.NotEmpty(result.Matches);
        var topMatch = result.Matches[0];
        Assert.Equal(DatabaseTuningContent, topMatch.Content);
        Assert.True(topMatch.Score > 0.0);
        Assert.Equal("database", topMatch.Metadata.Values["category"]);
        Assert.Equal("platform", topMatch.Metadata.Values["team"]);
    }

    [Fact]
    public async Task Execute_ShouldReturnOnlyRecordsMatchingMetadataFilter()
    {
        using var context = RetrievalIntegrationContext.Create();
        await SeedKnowledgeBaseAsync(context, PrimaryIndex);

        var result = await context.ExecuteVectorQueryToolAsync(
            PrimaryIndex,
            DatabaseTuningQuery,
            metadataFilter: RetrievalIntegrationContext.MetadataEquals("category", "database"));

        // Verifies the tool forwards the metadata filter so only records matching the criterion are returned.
        Assert.True(result.Succeeded);
        Assert.NotEmpty(result.Matches);
        Assert.All(result.Matches, match => Assert.Equal("database", match.Metadata.Values["category"]));
        Assert.Equal(
            new[] { DatabaseBackupId, DatabaseTuningId }.OrderBy(id => id, StringComparer.Ordinal),
            result.Matches.Select(match => match.RecordId).OrderBy(id => id, StringComparer.Ordinal));
    }

    [Fact]
    public async Task Execute_ShouldReturnEmptySuccess_WhenMetadataFilterMatchesNothing()
    {
        using var context = RetrievalIntegrationContext.Create();
        await SeedKnowledgeBaseAsync(context, PrimaryIndex);

        var result = await context.ExecuteVectorQueryToolAsync(
            PrimaryIndex,
            DatabaseTuningQuery,
            metadataFilter: RetrievalIntegrationContext.MetadataEquals("category", "no-such-category"));

        // Verifies a metadata filter matching no record yields a successful but empty tool result, not a failure.
        Assert.True(result.Succeeded);
        Assert.Equal(RetrievalErrorCode.None, result.ErrorCode);
        Assert.Empty(result.Matches);
    }

    [Fact]
    public async Task Execute_ShouldReturnEmptySuccess_WhenIndexHasNoRecords()
    {
        using var context = RetrievalIntegrationContext.Create();
        await context.CreateEmptyIndexAsync(EmptyIndex);

        var result = await context.ExecuteVectorQueryToolAsync(EmptyIndex, DatabaseTuningQuery);

        // Verifies an empty index yields a successful but empty tool result without throwing.
        Assert.True(result.Succeeded);
        Assert.Equal(RetrievalErrorCode.None, result.ErrorCode);
        Assert.Empty(result.Matches);
    }

    [Fact]
    public async Task Execute_ShouldReportDeterministicFailure_WhenIndexWasNeverCreated()
    {
        using var context = RetrievalIntegrationContext.Create();

        var result = await context.ExecuteVectorQueryToolAsync("missing-index", DatabaseTuningQuery);

        // Verifies a query against a never-created index surfaces a deterministic failure through the tool
        // contract rather than throwing a provider-specific exception.
        Assert.False(result.Succeeded);
        Assert.Equal(RetrievalErrorCode.VectorStoreQueryFailed, result.ErrorCode);
        Assert.Empty(result.Matches);
    }

    [Fact]
    public async Task Execute_ShouldReportInvalidRequest_WhenVectorStoreNameIsMissing()
    {
        using var context = RetrievalIntegrationContext.Create();
        await SeedKnowledgeBaseAsync(context, PrimaryIndex);

        var result = await context.ExecuteVectorQueryToolAsync(
            PrimaryIndex,
            DatabaseTuningQuery,
            vectorStoreName: "   ");

        // Verifies the tool's own boundary validation (missing vector store name) fails deterministically before
        // the retrieval pipeline is invoked.
        Assert.False(result.Succeeded);
        Assert.Equal(RetrievalErrorCode.InvalidRequest, result.ErrorCode);
        Assert.Empty(result.Matches);
    }

    /// <summary>
    /// Seeds the shared knowledge-base fixture into the target index: one strongly matching database-tuning
    /// record, one partially matching database-backup record, and two non-matching records, so similarity
    /// ordering, TopK limiting, and metadata filtering are all provable from the same deterministic data.
    /// </summary>
    /// <param name="context">The retrieval integration context to seed.</param>
    /// <param name="indexName">The index the knowledge-base records are written to.</param>
    private static Task SeedKnowledgeBaseAsync(RetrievalIntegrationContext context, string indexName)
    {
        return context.SeedAsync(
            indexName,
            new RetrievalSeedRecord(
                DatabaseTuningId,
                DatabaseTuningContent,
                new Dictionary<string, string> { ["category"] = "database", ["team"] = "platform" }),
            new RetrievalSeedRecord(
                DatabaseBackupId,
                DatabaseBackupContent,
                new Dictionary<string, string> { ["category"] = "database", ["team"] = "platform" }),
            new RetrievalSeedRecord(
                NetworkLatencyId,
                NetworkLatencyContent,
                new Dictionary<string, string> { ["category"] = "network", ["team"] = "platform" }),
            new RetrievalSeedRecord(
                CookingId,
                CookingContent,
                new Dictionary<string, string> { ["category"] = "cooking", ["team"] = "culinary" }));
    }
}


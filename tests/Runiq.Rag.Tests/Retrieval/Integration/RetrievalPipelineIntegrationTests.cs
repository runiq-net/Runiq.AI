using Runiq.Rag.Models.Retrieval;
using Runiq.Rag.Tests.Retrieval.Integration.Support;

namespace Runiq.Rag.Tests.Retrieval.Integration;

/// <summary>
/// End-to-end retrieval integration tests that drive the real RAG dependency injection graph — deterministic
/// keyword embedding, in-memory vector store, and the resolved retrieval pipeline — through the full
/// query text → embedding → vector search → filtered TopK result chain. No real embedding provider, network,
/// database, or external SDK is involved, and every scenario is deterministic.
/// </summary>
public sealed class RetrievalPipelineIntegrationTests
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
    public async Task Retrieve_ShouldEmbedQueryTextAndReturnClosestChunkFromTargetIndex()
    {
        using var context = RetrievalIntegrationContext.Create();
        await SeedKnowledgeBaseAsync(context, PrimaryIndex);

        var result = await context.RetrieveAsync(PrimaryIndex, DatabaseTuningQuery);

        // Verifies that retrieval generates an embedding from query text and returns the closest chunk from the target index.
        Assert.True(result.Succeeded);
        Assert.Equal(RetrievalErrorCode.None, result.ErrorCode);
        Assert.NotEmpty(result.Items);
        Assert.Equal(DatabaseTuningId, result.Items[0].RecordId);
    }

    [Fact]
    public async Task Retrieve_ShouldOnlyReturnRecordsStoredUnderRequestedIndexName()
    {
        using var context = RetrievalIntegrationContext.Create();
        await SeedKnowledgeBaseAsync(context, PrimaryIndex);
        await context.SeedAsync(SecondaryIndex, new RetrievalSeedRecord(
            "kb-secondary-database",
            DatabaseTuningContent,
            new Dictionary<string, string> { ["category"] = "database" }));

        var result = await context.RetrieveAsync(PrimaryIndex, DatabaseTuningQuery);

        // Verifies that retrieval only searches records stored under the requested index name.
        Assert.True(result.Succeeded);
        Assert.NotEmpty(result.Items);
        Assert.All(result.Items, item => Assert.NotEqual("kb-secondary-database", item.RecordId));
        Assert.Contains(result.Items, item => item.RecordId == DatabaseTuningId);
    }

    [Fact]
    public async Task Retrieve_ShouldKeepDifferentIndexesIsolated_WhenBothHoldSimilarRecords()
    {
        using var context = RetrievalIntegrationContext.Create();
        await context.SeedAsync(PrimaryIndex, new RetrievalSeedRecord(
            DatabaseTuningId,
            DatabaseTuningContent,
            new Dictionary<string, string> { ["index"] = "primary" }));
        await context.SeedAsync(SecondaryIndex, new RetrievalSeedRecord(
            "kb-secondary-copy",
            DatabaseTuningContent,
            new Dictionary<string, string> { ["index"] = "secondary" }));

        var result = await context.RetrieveAsync(SecondaryIndex, DatabaseTuningQuery);

        // Verifies that a record equally similar to the query in another index is never returned from the target index.
        Assert.True(result.Succeeded);
        Assert.NotEmpty(result.Items);
        Assert.All(result.Items, item => Assert.Equal("secondary", item.Metadata.Values["index"]));
        Assert.All(result.Items, item => Assert.NotEqual(DatabaseTuningId, item.RecordId));
    }

    [Fact]
    public async Task Retrieve_ShouldLimitResultCountToTopK()
    {
        using var context = RetrievalIntegrationContext.Create();
        await SeedKnowledgeBaseAsync(context, PrimaryIndex);

        var result = await context.RetrieveAsync(PrimaryIndex, DatabaseTuningQuery, topK: 2);

        // Verifies that TopK caps the number of returned matches even though the index holds more candidate records.
        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task Retrieve_ShouldOrderResultsBySimilarityScoreDescending()
    {
        using var context = RetrievalIntegrationContext.Create();
        await SeedKnowledgeBaseAsync(context, PrimaryIndex);

        var result = await context.RetrieveAsync(PrimaryIndex, DatabaseTuningQuery, topK: 2);

        // Verifies that the most similar records come first and results are ordered by descending similarity score.
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(DatabaseTuningId, result.Items[0].RecordId);
        Assert.Equal(DatabaseBackupId, result.Items[1].RecordId);
        Assert.True(result.Items[0].Score > result.Items[1].Score);
    }

    [Fact]
    public async Task Retrieve_ShouldReturnChunkContentOnResultItems()
    {
        using var context = RetrievalIntegrationContext.Create();
        await SeedKnowledgeBaseAsync(context, PrimaryIndex);

        var result = await context.RetrieveAsync(PrimaryIndex, DatabaseTuningQuery);

        // Verifies that the top retrieval match carries the stored chunk content.
        Assert.NotEmpty(result.Items);
        Assert.Equal(DatabaseTuningContent, result.Items[0].Content);
    }

    [Fact]
    public async Task Retrieve_ShouldReturnMetadataOnResultItems()
    {
        using var context = RetrievalIntegrationContext.Create();
        await SeedKnowledgeBaseAsync(context, PrimaryIndex);

        var result = await context.RetrieveAsync(PrimaryIndex, DatabaseTuningQuery);

        // Verifies that a retrieval match carries the expected metadata key/value stored with the record.
        Assert.NotEmpty(result.Items);
        Assert.Equal("database", result.Items[0].Metadata.Values["category"]);
        Assert.Equal("platform", result.Items[0].Metadata.Values["team"]);
    }

    [Fact]
    public async Task Retrieve_ShouldReturnSimilarityScoreWithinReasonableRange()
    {
        using var context = RetrievalIntegrationContext.Create();
        await SeedKnowledgeBaseAsync(context, PrimaryIndex);

        var result = await context.RetrieveAsync(PrimaryIndex, DatabaseTuningQuery);

        // Verifies that the similarity score is populated and, for the cosine higher-is-better contract, sits in the (0, 1] range.
        Assert.NotEmpty(result.Items);
        Assert.True(result.Items[0].Score > 0.0);
        Assert.True(result.Items[0].Score <= 1.0 + 1e-9);
    }

    [Fact]
    public async Task Retrieve_ShouldReturnOnlyRecordsMatchingMetadataFilter()
    {
        using var context = RetrievalIntegrationContext.Create();
        await SeedKnowledgeBaseAsync(context, PrimaryIndex);

        var result = await context.RetrieveAsync(
            PrimaryIndex,
            DatabaseTuningQuery,
            metadataFilter: RetrievalIntegrationContext.MetadataEquals("category", "database"));

        // Verifies that metadata filtering keeps only records whose metadata matches, excluding otherwise similar records.
        Assert.True(result.Succeeded);
        Assert.NotEmpty(result.Items);
        Assert.All(result.Items, item => Assert.Equal("database", item.Metadata.Values["category"]));
        Assert.Equal(
            new[] { DatabaseBackupId, DatabaseTuningId }.OrderBy(id => id, StringComparer.Ordinal),
            result.Items.Select(item => item.RecordId).OrderBy(id => id, StringComparer.Ordinal));
    }

    [Fact]
    public async Task Retrieve_ShouldReturnEmptyResult_WhenMetadataFilterMatchesNothing()
    {
        using var context = RetrievalIntegrationContext.Create();
        await SeedKnowledgeBaseAsync(context, PrimaryIndex);

        var result = await context.RetrieveAsync(
            PrimaryIndex,
            DatabaseTuningQuery,
            metadataFilter: RetrievalIntegrationContext.MetadataEquals("category", "no-such-category"));

        // Verifies that a metadata filter matching no record returns a successful but empty result without throwing.
        Assert.True(result.Succeeded);
        Assert.Equal(RetrievalErrorCode.None, result.ErrorCode);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task Retrieve_ShouldReturnEmptyResult_WhenIndexHasNoRecords()
    {
        using var context = RetrievalIntegrationContext.Create();
        await context.CreateEmptyIndexAsync(EmptyIndex);

        var result = await context.RetrieveAsync(EmptyIndex, DatabaseTuningQuery);

        // Verifies that an empty index returns an empty retrieval result without throwing.
        Assert.True(result.Succeeded);
        Assert.Equal(RetrievalErrorCode.None, result.ErrorCode);
        Assert.Empty(result.Items);
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

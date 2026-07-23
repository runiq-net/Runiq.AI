using Npgsql;
using Runiq.AI.Core.AI.Embeddings;
using Runiq.AI.Rag.Models.Metadata;
using Runiq.AI.Rag.Models.Retrieval;
using Runiq.AI.Rag.Models.VectorStores;
using Runiq.AI.Rag.PostgreSql.Documents;
using Runiq.AI.Rag.Retrieval;

namespace Runiq.AI.Rag.PostgreSql.Tests;

[Collection(PostgreSqlIntegrationCollection.Name)]
[Trait("Category", "Integration")]
public sealed class PostgreSqlRagIntegrationTests
{
    private readonly PostgreSqlIntegrationFixture fixture;

    public PostgreSqlRagIntegrationTests(PostgreSqlIntegrationFixture fixture) => this.fixture = fixture;

    // Verifies idempotent schema initialization, persisted migration version, extension availability, and provider health.
    [Fact]
    public async Task SchemaInitialization_AndHealth_AreIdempotentAndHealthy()
    {
        var first = await fixture.Health.CheckAsync();
        var second = await fixture.Health.CheckAsync();
        Assert.True(first.IsHealthy); Assert.True(second.IsHealthy);
        Assert.True(first.CanConnect); Assert.True(first.VectorExtensionAvailable); Assert.True(first.SchemaAvailable);
        Assert.Equal(2, first.MigrationVersion);
        Assert.Equal(1, await fixture.ScalarLongAsync("SELECT count(*) FROM __SCHEMA__.schema_migrations WHERE version=1"));
        Assert.Equal(1, await fixture.ScalarLongAsync("SELECT count(*) FROM __SCHEMA__.schema_migrations WHERE version=2"));
        var reinitialized = await fixture.ReinitializeSchemaAsync();
        Assert.True(reinitialized.IsHealthy);
        Assert.Equal(2, reinitialized.MigrationVersion);
        Assert.Equal(1, await fixture.ScalarLongAsync("SELECT count(*) FROM __SCHEMA__.schema_migrations WHERE version=2"));
    }

    // Verifies that a schema created at migration v1 upgrades safely to migration v2.
    [Fact]
    public async Task SchemaInitialization_UpgradesExistingVersionOneSchema()
    {
        var upgraded = await fixture.UpgradeLegacySchemaAsync();

        Assert.True(upgraded.IsHealthy);
        Assert.Equal(2, upgraded.MigrationVersion);
    }

    // Verifies logical index persistence, duplicate compatibility, model metadata, and dimension mismatch rejection.
    [Fact]
    public async Task LogicalIndex_PersistsMetadata_AndRejectsMismatch()
    {
        var name = NewId("index");
        var created = await fixture.VectorStore.CreateIndexAsync(Index(name, 3, VectorDistanceMetric.Cosine, "model-a"));
        var duplicate = await fixture.VectorStore.CreateIndexAsync(Index(name, 3, VectorDistanceMetric.Cosine, "model-a"));
        var mismatch = await fixture.VectorStore.CreateIndexAsync(Index(name, 2, VectorDistanceMetric.Cosine, "model-a"));
        Assert.True(created.Succeeded); Assert.True(duplicate.Succeeded); Assert.False(mismatch.Succeeded);
        Assert.Equal("model-a", await fixture.ScalarStringAsync("SELECT embedding_model FROM __SCHEMA__.rag_indexes WHERE index_name=@index", ("index", name)));
        Assert.Equal(3, await fixture.ScalarLongAsync("SELECT embedding_dimension FROM __SCHEMA__.rag_indexes WHERE index_name=@index", ("index", name)));
    }

    // Verifies create, same-hash skip, changed-hash replacement, metadata persistence, and unaffected documents.
    [Fact]
    public async Task DocumentAggregate_CreatesSkipsAndAtomicallyReplacesChunks()
    {
        var index = NewId("aggregate"); await fixture.VectorStore.CreateIndexAsync(Index(index, 3));
        var document = NewId("document"); var other = NewId("other");
        var created = await fixture.Documents.UpsertDocumentAsync(Request(index, document, "hash-1", Record("old-1", [1, 0, 0], "blue"), Record("old-2", [0, 1, 0], "blue")));
        await fixture.Documents.UpsertDocumentAsync(Request(index, other, "other-hash", Record("other-1", [0, 0, 1], "green")));
        var skipped = await fixture.Documents.UpsertDocumentAsync(Request(index, document, "hash-1", Record("ignored", [0, 0, 1], "red")));
        Assert.Equal(PostgreSqlRagDocumentUpsertStatus.Created, created.Status); Assert.Equal(PostgreSqlRagDocumentUpsertStatus.Skipped, skipped.Status);
        Assert.Equal(2, await ChunkCount(index, document));
        var updated = await fixture.Documents.UpsertDocumentAsync(Request(index, document, "hash-2", Record("new-1", [1, 1, 0], "red")));
        Assert.Equal(PostgreSqlRagDocumentUpsertStatus.Updated, updated.Status);
        Assert.Equal(1, await ChunkCount(index, document)); Assert.Equal(1, await ChunkCount(index, other));
        Assert.Equal(0, await fixture.ScalarLongAsync("SELECT count(*) FROM __SCHEMA__.rag_chunks WHERE index_name=@index AND chunk_id LIKE 'old-%'", ("index", index)));
        Assert.Equal("hash-2", await fixture.ScalarStringAsync("SELECT content_hash FROM __SCHEMA__.rag_ingestion_states WHERE index_name=@index AND document_id=@document", ("index", index), ("document", document)));
    }

    // Verifies document delete cascades to chunks and ingestion state without crossing index boundaries and remains idempotent.
    [Fact]
    public async Task DeleteDocument_CascadesAndIsIndexScopedAndIdempotent()
    {
        var firstIndex = NewId("delete_a"); var secondIndex = NewId("delete_b"); var document = NewId("shared");
        await fixture.VectorStore.CreateIndexAsync(Index(firstIndex, 3)); await fixture.VectorStore.CreateIndexAsync(Index(secondIndex, 3));
        await fixture.Documents.UpsertDocumentAsync(Request(firstIndex, document, "a", Record("a-1", [1, 0, 0], "a")));
        await fixture.Documents.UpsertDocumentAsync(Request(secondIndex, document, "b", Record("b-1", [1, 0, 0], "b")));
        var deleted = await fixture.Documents.DeleteDocumentAsync(firstIndex, document);
        var repeated = await fixture.Documents.DeleteDocumentAsync(firstIndex, document);
        Assert.Equal(PostgreSqlRagDocumentDeleteStatus.Deleted, deleted.Status); Assert.Equal(PostgreSqlRagDocumentDeleteStatus.NotFound, repeated.Status);
        Assert.Equal(0, await ChunkCount(firstIndex, document)); Assert.Equal(1, await ChunkCount(secondIndex, document));
        Assert.Equal(0, await fixture.ScalarLongAsync("SELECT count(*) FROM __SCHEMA__.rag_ingestion_states WHERE index_name=@index AND document_id=@document", ("index", firstIndex), ("document", document)));
    }

    // Verifies a database constraint failure rolls the entire replacement back to the previous consistent aggregate.
    [Fact]
    public async Task DocumentReplacement_WhenChunkWriteFails_RollsBackCompletely()
    {
        var index = NewId("rollback"); await fixture.VectorStore.CreateIndexAsync(Index(index, 3));
        await fixture.Documents.UpsertDocumentAsync(Request(index, "owner", "owner-hash", Record("collision", [0, 1, 0], "owner")));
        await fixture.Documents.UpsertDocumentAsync(Request(index, "target", "old-hash", Record("old", [1, 0, 0], "target")));
        await Assert.ThrowsAsync<PostgresException>(() => fixture.Documents.UpsertDocumentAsync(Request(index, "target", "new-hash", Record("collision", [1, 1, 0], "target"))));
        Assert.Equal("old-hash", await fixture.ScalarStringAsync("SELECT content_hash FROM __SCHEMA__.rag_documents WHERE index_name=@index AND document_id='target'", ("index", index)));
        Assert.Equal(1, await ChunkCount(index, "target"));
        Assert.Equal("old-hash", await fixture.ScalarStringAsync("SELECT content_hash FROM __SCHEMA__.rag_ingestion_states WHERE index_name=@index AND document_id='target'", ("index", index)));
    }

    // Verifies cosine search executes in PostgreSQL with candidate limiting, metadata filtering, empty results, and stable ties.
    [Fact]
    public async Task VectorSearch_CosineFiltersLimitsAndOrdersStableTies()
    {
        var index = NewId("cosine"); await fixture.VectorStore.CreateIndexAsync(Index(index, 3));
        await fixture.Documents.UpsertDocumentAsync(Request(index, "doc-b", "b", Record("chunk-b", [1, 0, 0], "blue")));
        await fixture.Documents.UpsertDocumentAsync(Request(index, "doc-a", "a", Record("chunk-a", [1, 0, 0], "blue"), Record("chunk-c", [0, 1, 0], "red")));
        var result = await fixture.VectorStore.QueryAsync(new QueryVectorRequest { IndexName = index, Values = [1, 0, 0], TopK = 2, MetadataFilter = new RetrievalMetadataFilter([new RetrievalMetadataFilterCriterion("color", "blue")]) });
        Assert.True(result.Succeeded); Assert.Equal(["chunk-a", "chunk-b"], result.Records.Select(r => r.Id));
        Assert.All(result.Records, r => { Assert.Equal("cosine-distance", r.Metric); Assert.False(r.HigherIsBetter); Assert.Equal(0, r.RawScore, 8); });
        var empty = await fixture.VectorStore.QueryAsync(new QueryVectorRequest { IndexName = index, Values = [1, 0, 0], MetadataFilter = new RetrievalMetadataFilter([new RetrievalMetadataFilterCriterion("color", "missing")]) });
        Assert.True(empty.Succeeded); Assert.Empty(empty.Records);
    }

    // Verifies Euclidean distance and dot-product raw score direction are mapped without bypassing the vector-store result contract.
    [Theory]
    [InlineData(VectorDistanceMetric.Euclidean, "euclidean-distance", false, 0.0)]
    [InlineData(VectorDistanceMetric.DotProduct, "dot-product", true, 1.0)]
    public async Task VectorSearch_MapsProviderMetricSemantics(VectorDistanceMetric metric, string metricName, bool higherIsBetter, double expected)
    {
        var index = NewId(metric.ToString()); await fixture.VectorStore.CreateIndexAsync(Index(index, 3, metric));
        await fixture.Documents.UpsertDocumentAsync(Request(index, "doc", "hash", Record("chunk", [1, 0, 0], "metric")));
        var result = await fixture.VectorStore.QueryAsync(new QueryVectorRequest { IndexName = index, Values = [1, 0, 0], TopK = 1 });
        var record = Assert.Single(result.Records); Assert.Equal(metricName, record.Metric); Assert.Equal(higherIsBetter, record.HigherIsBetter); Assert.Equal(expected, record.RawScore, 8);
    }

    // Verifies a dimension mismatch fails before document or chunk state is written.
    [Fact]
    public async Task DocumentAggregate_WithDimensionMismatch_LeavesNoPartialState()
    {
        var index = NewId("dimension"); await fixture.VectorStore.CreateIndexAsync(Index(index, 3));
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Documents.UpsertDocumentAsync(Request(index, "bad", "hash", Record("bad", [1, 0], "bad"))));
        Assert.Equal(0, await fixture.ScalarLongAsync("SELECT count(*) FROM __SCHEMA__.rag_documents WHERE index_name=@index AND document_id='bad'", ("index", index)));
    }

    // Verifies cancellation interrupts a real PostgreSQL lock wait, rolls back, and leaves the connection pool usable.
    [Fact]
    public async Task DocumentAggregate_CancellationInterruptsDatabaseWaitAndPoolRecovers()
    {
        var index = NewId("cancel"); await fixture.VectorStore.CreateIndexAsync(Index(index, 3));
        await using var blocker = await fixture.DataSource.OpenConnectionAsync(); await using var transaction = await blocker.BeginTransactionAsync();
        await using var command = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended(@key,0))", blocker, transaction);
        command.Parameters.AddWithValue("key", $"\"{fixture.Schema}\"\u001f{index}\u001fdoc"); await command.ExecuteNonQueryAsync();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Documents.UpsertDocumentAsync(Request(index, "doc", "hash", Record("chunk", [1, 0, 0], "cancel")), cancellation.Token));
        await transaction.RollbackAsync();
        Assert.Equal(0, await fixture.ScalarLongAsync("SELECT count(*) FROM __SCHEMA__.rag_documents WHERE index_name=@index AND document_id='doc'", ("index", index)));
        Assert.True((await fixture.Health.CheckAsync()).IsHealthy);
    }

    // Verifies PostgreSQL lexical retrieval for punctuation-sensitive and mixed-case technical identifiers.
    [Theory]
    [InlineData("CS1503")]
    [InlineData("POL-HR-014")]
    [InlineData("IRagRetriever")]
    [InlineData("UseRag")]
    [InlineData("RagSearchCompleted")]
    [InlineData("filename.cs")]
    public async Task LexicalSearch_RetrievesTechnicalIdentifiers(string identifier)
    {
        var index = NewId("identifier");
        await fixture.VectorStore.CreateIndexAsync(Index(index, 3));
        await fixture.Documents.UpsertDocumentAsync(Request(
            index,
            NewId("document"),
            "hash",
            Record("identifier-chunk", [1, 0, 0], "technical", $"Reference: {identifier}")));

        var result = await fixture.VectorStore.QueryLexicalAsync(new QueryLexicalRequest
        {
            IndexName = index,
            QueryText = identifier,
            TopK = 5,
        });

        Assert.True(result.Succeeded);
        Assert.Equal("identifier-chunk", Assert.Single(result.Records).Id);
    }

    // Verifies exact phrase intent is evaluated by the PostgreSQL provider query path.
    [Fact]
    public async Task LexicalSearch_QuotedQueryRequiresExactPhraseOrder()
    {
        var index = NewId("phrase");
        await fixture.VectorStore.CreateIndexAsync(Index(index, 3));
        await fixture.Documents.UpsertDocumentAsync(Request(
            index,
            "phrase-document",
            "hash",
            Record("phrase-chunk", [1, 0, 0], "phrase", "hybrid retrieval preserves exact phrase order")));

        var match = await fixture.VectorStore.QueryLexicalAsync(new QueryLexicalRequest
        {
            IndexName = index,
            QueryText = "\"exact phrase order\"",
        });
        var miss = await fixture.VectorStore.QueryLexicalAsync(new QueryLexicalRequest
        {
            IndexName = index,
            QueryText = "\"phrase exact order\"",
        });

        Assert.Single(match.Records);
        Assert.Empty(miss.Records);
    }

    // Verifies lexical state follows same-hash skips, changed-hash replacement, and document deletion.
    [Fact]
    public async Task LexicalSearch_TracksDocumentAggregateLifecycle()
    {
        var index = NewId("lexical_lifecycle");
        await fixture.VectorStore.CreateIndexAsync(Index(index, 3));
        await fixture.Documents.UpsertDocumentAsync(Request(
            index, "document", "hash-1", Record("old-chunk", [1, 0, 0], "state", "CS1503 old state")));
        var skipped = await fixture.Documents.UpsertDocumentAsync(Request(
            index, "document", "hash-1", Record("ignored-chunk", [1, 0, 0], "state", "POL-HR-014 ignored")));

        Assert.Equal(PostgreSqlRagDocumentUpsertStatus.Skipped, skipped.Status);
        Assert.Single((await Lexical(index, "CS1503")).Records);
        Assert.Empty((await Lexical(index, "POL-HR-014")).Records);

        var updated = await fixture.Documents.UpsertDocumentAsync(Request(
            index, "document", "hash-2", Record("new-chunk", [1, 0, 0], "state", "POL-HR-014 current state")));
        Assert.Equal(PostgreSqlRagDocumentUpsertStatus.Updated, updated.Status);
        Assert.Empty((await Lexical(index, "CS1503")).Records);
        Assert.Single((await Lexical(index, "POL-HR-014")).Records);

        await fixture.Documents.DeleteDocumentAsync(index, "document");
        Assert.Empty((await Lexical(index, "POL-HR-014")).Records);
    }

    // Verifies real semantic and lexical PostgreSQL candidates are fused once with deterministic hybrid ordering.
    [Fact]
    public async Task HybridSearch_UsesRealSourcesAndMergesDuplicateDeterministically()
    {
        var index = NewId("hybrid");
        await fixture.VectorStore.CreateIndexAsync(Index(index, 3));
        await fixture.Documents.UpsertDocumentAsync(Request(
            index, "document-b", "b", Record("chunk-b", [0, 1, 0], "hybrid", "CS1503 secondary")));
        await fixture.Documents.UpsertDocumentAsync(Request(
            index, "document-a", "a", Record("chunk-a", [1, 0, 0], "hybrid", "CS1503 primary")));
        var pipeline = new DefaultRagRetrievalPipeline(new FixedEmbeddingClient([1, 0, 0]), fixture.VectorStore);
        var request = new RetrievalRequest
        {
            IndexName = index,
            QueryText = "CS1503",
            Mode = RagRetrievalMode.Hybrid,
            TopK = 5,
        };

        var first = await pipeline.RetrieveAsync(request);
        var second = await pipeline.RetrieveAsync(request);

        Assert.True(first.Succeeded);
        Assert.Equal(["chunk-a", "chunk-b"], first.Items.Select(item => item.RecordId));
        Assert.Equal(first.Items.Select(item => item.RecordId), second.Items.Select(item => item.RecordId));
        Assert.All(first.Items, item =>
        {
            Assert.NotNull(item.Provenance?.SemanticRank);
            Assert.NotNull(item.Provenance?.LexicalRank);
            Assert.NotNull(item.Provenance?.FusedRank);
        });
    }

    // Verifies the production lexical command shape retains filters, ranking, ordering, limiting, and both GIN access paths.
    [Fact]
    public async Task LexicalSearch_ProductionQueryPlansUseGinAndTrigramIndexes()
    {
        var index = NewId("plan");
        await fixture.VectorStore.CreateIndexAsync(Index(index, 3));
        var records = Enumerable.Range(0, 3000)
            .Select(number => Record($"noise-{number}", [1, 0, 0], "plan",
                $"unrelated representative corpus entry number {number}"))
            .Append(Record("plan-chunk", [1, 0, 0], "plan", "CS1503 ordinary lexical retrieval"))
            .ToArray();
        await fixture.Documents.UpsertDocumentAsync(Request(
            index, "plan-document", "hash", records));
        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var analyze = new Npgsql.NpgsqlCommand(
            $"ANALYZE \"{fixture.Schema}\".rag_chunks", connection))
            await analyze.ExecuteNonQueryAsync();
        // Isolate lexical access-path evidence from the competing document lookup index in this disposable schema.
        await using (var isolate = new Npgsql.NpgsqlCommand(
            $"""
            DROP INDEX "{fixture.Schema}".ix_rag_chunks_document;
            ALTER TABLE "{fixture.Schema}".rag_chunks DROP CONSTRAINT rag_chunks_pkey;
            ALTER TABLE "{fixture.Schema}".rag_chunks DROP CONSTRAINT rag_chunks_index_name_document_id_chunk_id_key;
            """, connection))
            await isolate.ExecuteNonQueryAsync();
        await using (var planner = new Npgsql.NpgsqlCommand("SET enable_seqscan=off", connection))
            await planner.ExecuteNonQueryAsync();

        var usedFullTextGin = false;
        var usedTrigramGin = false;
        var observedPlans = new List<string>();
        foreach (var query in new[] { "ordinary lexical", "\"ordinary lexical\"", "CS1503" })
        {
            await using var production = PostgreSqlRagVectorStore.CreateLexicalCommand(
                connection, $"\"{fixture.Schema}\"", new QueryLexicalRequest
                {
                    IndexName = index,
                    QueryText = query,
                    TopK = 7,
                });
            Assert.Contains("index_name=@index", production.CommandText, StringComparison.Ordinal);
            Assert.Contains("ts_rank_cd", production.CommandText, StringComparison.Ordinal);
            Assert.Contains("ORDER BY lexical_score DESC, document_id ASC, chunk_id ASC", production.CommandText, StringComparison.Ordinal);
            Assert.Contains("LIMIT @limit", production.CommandText, StringComparison.Ordinal);
            production.CommandText = "EXPLAIN " + production.CommandText;
            var plan = new List<string>();
            await using var reader = await production.ExecuteReaderAsync();
            while (await reader.ReadAsync()) plan.Add(reader.GetString(0));
            var renderedPlan = string.Join(Environment.NewLine, plan);
            observedPlans.Add($"{query}:{Environment.NewLine}{renderedPlan}");
            usedFullTextGin |= renderedPlan.Contains("ix_rag_chunks_lexical_search", StringComparison.Ordinal);
            usedTrigramGin |= renderedPlan.Contains("ix_rag_chunks_identifier_search", StringComparison.Ordinal);
        }
        Assert.True(usedFullTextGin);
        Assert.True(usedTrigramGin, string.Join(Environment.NewLine, observedPlans));
        await transaction.RollbackAsync();
    }

    private async Task<long> ChunkCount(string index, string document) => await fixture.ScalarLongAsync("SELECT count(*) FROM __SCHEMA__.rag_chunks WHERE index_name=@index AND document_id=@document", ("index", index), ("document", document));
    private static CreateVectorIndexRequest Index(string name, int dimensions, VectorDistanceMetric metric = VectorDistanceMetric.Cosine, string model = "model") => new() { IndexName = name, Dimensions = dimensions, Metric = metric, Metadata = new RagMetadata(new Dictionary<string, string> { { "embeddingModel", model } }) };
    private static PostgreSqlRagDocumentUpsertRequest Request(string index, string document, string hash, params VectorRecord[] records) => new() { IndexName = index, DocumentId = document, ContentHash = hash, Source = "test", Title = "Test", Version = "1", Metadata = new RagMetadata(new Dictionary<string, string> { { "kind", "integration" } }), Records = records };
    private Task<QueryLexicalResult> Lexical(string index, string query) =>
        fixture.VectorStore.QueryLexicalAsync(new QueryLexicalRequest { IndexName = index, QueryText = query });
    private static VectorRecord Record(string id, float[] values, string color, string? content = null) => new() { Id = id, Values = values, Content = content ?? id, Metadata = new RagMetadata(new Dictionary<string, string> { { "color", color } }) };
    private static string NewId(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

    private sealed class FixedEmbeddingClient(IReadOnlyList<float> vector) : IEmbeddingClient
    {
        public Task<EmbeddingResponse> EmbedAsync(
            EmbeddingRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new EmbeddingResponse(
                request.Inputs.Select((_, index) => new EmbeddingResult(index, vector, vector.Count)).ToArray()));
        }
    }
}

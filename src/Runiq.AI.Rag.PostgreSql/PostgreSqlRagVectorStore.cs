using System.Text.Json;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;
using Pgvector;
using Runiq.AI.Rag.Abstractions.VectorStores;
using Runiq.AI.Rag.Models.Documents;
using Runiq.AI.Rag.Models.Embeddings;
using Runiq.AI.Rag.Models.Metadata;
using Runiq.AI.Rag.Models.Queries;
using Runiq.AI.Rag.Models.Retrieval;
using Runiq.AI.Rag.Models.Search;
using Runiq.AI.Rag.Models.VectorStores;

namespace Runiq.AI.Rag.PostgreSql;

/// <summary>Persists RAG vectors in PostgreSQL and executes similarity search through pgvector.</summary>
internal sealed class PostgreSqlRagVectorStore : IRagVectorStore
{
    private readonly NpgsqlDataSource dataSource;
    private readonly PostgreSqlSchemaManager schema;

    public PostgreSqlRagVectorStore(NpgsqlDataSource dataSource, PostgreSqlSchemaManager schemaManager)
    { this.dataSource = dataSource; schema = schemaManager; }

    /// <inheritdoc />
    public async Task<CreateVectorIndexResult> CreateIndexAsync(CreateVectorIndexRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.IndexName) || request.Dimensions <= 0 || !Enum.IsDefined(request.Metric))
            return new CreateVectorIndexResult { IndexName = request.IndexName ?? string.Empty, Succeeded = false, Reason = "Index name, dimensions, and metric must be valid." };
        await schema.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var metric = MetricName(request.Metric);
        var sql = $"INSERT INTO {schema.QuotedSchema}.rag_indexes(index_name,embedding_model,embedding_dimension,metric,metadata) VALUES (@name,@model,@dimensions,@metric,@metadata) ON CONFLICT(index_name) DO UPDATE SET updated_at=now() WHERE rag_indexes.embedding_dimension=EXCLUDED.embedding_dimension AND rag_indexes.metric=EXCLUDED.metric AND rag_indexes.embedding_model=EXCLUDED.embedding_model RETURNING index_name";
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("name", request.IndexName);
        command.Parameters.AddWithValue("model", request.Metadata.Values.TryGetValue("embeddingModel", out var model) ? model : string.Empty);
        command.Parameters.AddWithValue("dimensions", request.Dimensions);
        command.Parameters.AddWithValue("metric", metric);
        command.Parameters.Add(new NpgsqlParameter("metadata", NpgsqlDbType.Jsonb) { Value = JsonSerializer.Serialize(request.Metadata.Values) });
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return new CreateVectorIndexResult { IndexName = request.IndexName, Succeeded = value is not null, Reason = value is null ? "Existing index metadata is incompatible." : string.Empty };
    }

    /// <inheritdoc />
    public async Task<UpsertVectorResult> UpsertAsync(UpsertVectorRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Records.Count == 0) return Failure(request, "At least one vector record is required.");
        await schema.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var dimensions = await ReadDimensionsAsync(connection, transaction, request.IndexName, cancellationToken).ConfigureAwait(false);
            if (dimensions is null || request.Records.Any(r => r.Values.Count != dimensions)) return Failure(request, dimensions is null ? "Vector index has not been created." : "Vector dimension does not match the index dimensions.");
            foreach (var record in request.Records)
            {
                var documentId = record.Metadata.Values.TryGetValue("documentId", out var id) ? id : record.Id;
                var hash = request.Metadata.Values.TryGetValue("contentHash", out var h) ? h : documentId;
                var documentSql = $"INSERT INTO {schema.QuotedSchema}.rag_documents(index_name,document_id,content_hash,metadata) VALUES(@index,@document,@hash,'{{}}'::jsonb) ON CONFLICT(index_name,document_id) DO UPDATE SET content_hash=EXCLUDED.content_hash,updated_at=now()";
                await using (var document = new NpgsqlCommand(documentSql, connection, transaction))
                { document.Parameters.AddWithValue("index", request.IndexName); document.Parameters.AddWithValue("document", documentId); document.Parameters.AddWithValue("hash", hash); await document.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false); }
                var chunkSql = $"INSERT INTO {schema.QuotedSchema}.rag_chunks(index_name,document_id,chunk_id,content,chunk_order,token_count,metadata,embedding) VALUES(@index,@document,@chunk,@content,@order,@tokens,@metadata,@embedding) ON CONFLICT(index_name,chunk_id) DO UPDATE SET document_id=EXCLUDED.document_id,content=EXCLUDED.content,chunk_order=EXCLUDED.chunk_order,token_count=EXCLUDED.token_count,metadata=EXCLUDED.metadata,embedding=EXCLUDED.embedding,updated_at=now()";
                await using var chunk = new NpgsqlCommand(chunkSql, connection, transaction);
                chunk.Parameters.AddWithValue("index", request.IndexName); chunk.Parameters.AddWithValue("document", documentId); chunk.Parameters.AddWithValue("chunk", record.Id); chunk.Parameters.AddWithValue("content", record.Content);
                chunk.Parameters.AddWithValue("order", ParseInt(record.Metadata, "chunkIndex") ?? 0); chunk.Parameters.AddWithValue("tokens", (object?)ParseInt(record.Metadata, "tokenCount") ?? DBNull.Value);
                chunk.Parameters.Add(new NpgsqlParameter("metadata", NpgsqlDbType.Jsonb) { Value = JsonSerializer.Serialize(record.Metadata.Values) }); chunk.Parameters.AddWithValue("embedding", new Vector(record.Values.ToArray()));
                await chunk.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                var stateSql = $"INSERT INTO {schema.QuotedSchema}.rag_ingestion_states(index_name,document_id,content_hash,status,ingested_at) VALUES(@index,@document,@hash,'completed',now()) ON CONFLICT(index_name,document_id) DO UPDATE SET content_hash=EXCLUDED.content_hash,status='completed',ingested_at=now(),failure_reason=NULL";
                await using var state = new NpgsqlCommand(stateSql, connection, transaction); state.Parameters.AddWithValue("index", request.IndexName); state.Parameters.AddWithValue("document", documentId); state.Parameters.AddWithValue("hash", hash); await state.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new UpsertVectorResult { Succeeded = true, AttemptedCount = request.Records.Count, ProcessedCount = request.Records.Count, VectorIds = request.Records.Select(r => r.Id).ToList() };
        }
        catch { await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false); throw; }
    }

    /// <inheritdoc />
    public async Task<DeleteVectorResult> DeleteAsync(DeleteVectorRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request); await schema.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var sql = $"DELETE FROM {schema.QuotedSchema}.rag_chunks WHERE index_name=@index AND chunk_id=ANY(@ids) RETURNING chunk_id";
        await using var command = new NpgsqlCommand(sql, connection); command.Parameters.AddWithValue("index", request.IndexName); command.Parameters.AddWithValue("ids", request.VectorIds.ToArray());
        var deleted = new List<string>(); await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false); while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) deleted.Add(reader.GetString(0));
        return new DeleteVectorResult { Succeeded = true, RequestedCount = request.VectorIds.Count, DeletedCount = deleted.Count, NotFoundVectorIds = request.VectorIds.Except(deleted, StringComparer.Ordinal).ToList() };
    }

    /// <inheritdoc />
    public async Task<QueryVectorResult> QueryAsync(QueryVectorRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request); await schema.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        if (request.TopK <= 0) return new QueryVectorResult { Succeeded = false, Reason = "TopK must be greater than zero." };
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var metric = await ReadMetricAsync(connection, request.IndexName, cancellationToken).ConfigureAwait(false); if (metric is null) return new QueryVectorResult { Succeeded = false, Reason = "Vector index has not been created." };
        var op = metric.Value switch { VectorDistanceMetric.Cosine => "<=>", VectorDistanceMetric.DotProduct => "<#>", _ => "<->" };
        var clauses = new List<string> { "index_name=@index" }; await using var command = new NpgsqlCommand { Connection = connection };
        command.Parameters.AddWithValue("index", request.IndexName); command.Parameters.AddWithValue("embedding", new Vector(request.Values.ToArray())); command.Parameters.AddWithValue("limit", request.TopK);
        for (var i = 0; i < request.MetadataFilter.Criteria.Count; i++)
        {
            var criterion = request.MetadataFilter.Criteria[i]; if (criterion.Operator != RetrievalMetadataFilterOperator.Equal) return new QueryVectorResult { Succeeded = false, Reason = "Metadata filter operator is not supported." };
            clauses.Add($"metadata ->> @key{i} = @value{i}"); command.Parameters.AddWithValue($"key{i}", criterion.Key); command.Parameters.AddWithValue($"value{i}", criterion.Value);
        }
        command.CommandText = $"SELECT chunk_id,content,metadata,embedding {op} @embedding AS distance FROM {schema.QuotedSchema}.rag_chunks WHERE {string.Join(" AND ", clauses)} ORDER BY distance ASC, document_id ASC, chunk_id ASC LIMIT @limit";
        var records = new List<VectorSearchResult>(); await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var distance = reader.GetDouble(3); var raw = metric == VectorDistanceMetric.DotProduct ? -distance : distance;
            if (!double.IsFinite(raw)) return new QueryVectorResult { Succeeded = false, Reason = "Provider returned an invalid score." };
            var metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(2)) ?? [];
            records.Add(new VectorSearchResult { Id = reader.GetString(0), Content = reader.GetString(1), Metadata = request.IncludeMetadata ? new RagMetadata(metadata) : RagMetadata.Empty, RawScore = raw, Relevance = Normalize(metric.Value, raw), Metric = ScoreMetric(metric.Value), HigherIsBetter = metric == VectorDistanceMetric.DotProduct });
        }
        return new QueryVectorResult { Succeeded = true, Records = records };
    }

    /// <inheritdoc />
    public async Task<UpsertVectorResult> UpsertAsync(string indexName, RagChunk chunk, RagEmbedding embedding, CancellationToken cancellationToken = default) => await ((IRagVectorStore)this).UpsertAsync(indexName, chunk, embedding, cancellationToken).ConfigureAwait(false);
    /// <inheritdoc />
    public async Task<IReadOnlyList<RagSearchResult>> SearchAsync(RagQuery query, RagEmbedding embedding, CancellationToken cancellationToken = default)
    {
        var result = await QueryAsync(new QueryVectorRequest { IndexName = query.IndexName ?? string.Empty, Values = embedding.Values, TopK = query.TopK }, cancellationToken).ConfigureAwait(false);
        return result.Records.Select(r => new RagSearchResult
        {
            Chunk = new RagChunk { Id = r.Id, DocumentId = r.Metadata.Values.TryGetValue("documentId", out var documentId) ? documentId : r.Id, Content = r.Content },
            RawScore = r.RawScore,
            Relevance = r.Relevance,
            Metric = r.Metric,
            HigherIsBetter = r.HigherIsBetter,
            Metadata = r.Metadata,
        }).ToList();
    }

    private async Task<int?> ReadDimensionsAsync(NpgsqlConnection c, NpgsqlTransaction t, string index, CancellationToken ct) { await using var cmd = new NpgsqlCommand($"SELECT embedding_dimension FROM {schema.QuotedSchema}.rag_indexes WHERE index_name=@index", c, t); cmd.Parameters.AddWithValue("index", index); return await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false) as int?; }
    private async Task<VectorDistanceMetric?> ReadMetricAsync(NpgsqlConnection c, string index, CancellationToken ct) { await using var cmd = new NpgsqlCommand($"SELECT metric FROM {schema.QuotedSchema}.rag_indexes WHERE index_name=@index", c); cmd.Parameters.AddWithValue("index", index); var value = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false) as string; return value switch { "cosine" => VectorDistanceMetric.Cosine, "dot_product" => VectorDistanceMetric.DotProduct, "euclidean" => VectorDistanceMetric.Euclidean, _ => null }; }
    private static string MetricName(VectorDistanceMetric metric) => metric switch { VectorDistanceMetric.Cosine => "cosine", VectorDistanceMetric.DotProduct => "dot_product", VectorDistanceMetric.Euclidean => "euclidean", _ => throw new ArgumentOutOfRangeException(nameof(metric)) };
    private static string ScoreMetric(VectorDistanceMetric metric) => metric switch { VectorDistanceMetric.Cosine => "cosine-distance", VectorDistanceMetric.DotProduct => RagScoreMetrics.DotProduct, _ => RagScoreMetrics.EuclideanDistance };
    private static double? Normalize(VectorDistanceMetric metric, double raw) => metric switch { VectorDistanceMetric.Cosine => 1 - raw / 2, VectorDistanceMetric.Euclidean => 1 / (1 + raw), _ => null };
    private static int? ParseInt(RagMetadata metadata, string key) => metadata.Values.TryGetValue(key, out var value) && int.TryParse(value, out var result) ? result : null;
    private static UpsertVectorResult Failure(UpsertVectorRequest request, string reason) => new() { Succeeded = false, AttemptedCount = request.Records.Count, FailedCount = request.Records.Count, Reason = reason, ErrorCode = VectorStoreUpsertErrorCode.StoreFailed };
}

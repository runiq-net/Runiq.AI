using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using Pgvector;

namespace Runiq.AI.Rag.PostgreSql.Documents;

internal sealed class PostgreSqlRagDocumentStore : IPostgreSqlRagDocumentStore
{
    private readonly NpgsqlDataSource dataSource;
    private readonly PostgreSqlSchemaManager schema;

    public PostgreSqlRagDocumentStore(NpgsqlDataSource dataSource, PostgreSqlSchemaManager schema)
    {
        this.dataSource = dataSource;
        this.schema = schema;
    }

    public async Task<PostgreSqlRagDocumentUpsertResult> UpsertDocumentAsync(PostgreSqlRagDocumentUpsertRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request);
        await schema.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await LockAggregateAsync(connection, transaction, request.IndexName, request.DocumentId, cancellationToken).ConfigureAwait(false);
            var dimensions = await ReadDimensionsAsync(connection, transaction, request.IndexName, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"RAG index '{request.IndexName}' does not exist.");
            if (request.Records.Any(record => record.Values.Count != dimensions))
                throw new ArgumentException($"Every embedding must have the index dimension of {dimensions}.", nameof(request));

            var existingHash = await ReadHashAsync(connection, transaction, request.IndexName, request.DocumentId, cancellationToken).ConfigureAwait(false);
            if (StringComparer.Ordinal.Equals(existingHash, request.ContentHash))
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return new PostgreSqlRagDocumentUpsertResult { Status = PostgreSqlRagDocumentUpsertStatus.Skipped };
            }

            var documentSql = $"""
                INSERT INTO {schema.QuotedSchema}.rag_documents(index_name,document_id,source,title,content_hash,version,metadata)
                VALUES(@index,@document,@source,@title,@hash,@version,@metadata)
                ON CONFLICT(index_name,document_id) DO UPDATE SET source=EXCLUDED.source,title=EXCLUDED.title,
                content_hash=EXCLUDED.content_hash,version=EXCLUDED.version,metadata=EXCLUDED.metadata,updated_at=now()
                """;
            await using (var document = new NpgsqlCommand(documentSql, connection, transaction))
            {
                document.Parameters.AddWithValue("index", request.IndexName);
                document.Parameters.AddWithValue("document", request.DocumentId);
                document.Parameters.AddWithValue("source", request.Source);
                document.Parameters.AddWithValue("title", request.Title);
                document.Parameters.AddWithValue("hash", request.ContentHash);
                document.Parameters.AddWithValue("version", request.Version);
                document.Parameters.Add(new NpgsqlParameter("metadata", NpgsqlDbType.Jsonb) { Value = Serialize(request.Metadata.Values) });
                await document.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            if (existingHash is not null)
            {
                await using var delete = new NpgsqlCommand($"DELETE FROM {schema.QuotedSchema}.rag_chunks WHERE index_name=@index AND document_id=@document", connection, transaction);
                delete.Parameters.AddWithValue("index", request.IndexName);
                delete.Parameters.AddWithValue("document", request.DocumentId);
                await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await InsertChunksAsync(connection, transaction, request, cancellationToken).ConfigureAwait(false);
            var stateSql = $"""
                INSERT INTO {schema.QuotedSchema}.rag_ingestion_states(index_name,document_id,content_hash,version,status,ingested_at,failure_reason)
                VALUES(@index,@document,@hash,@version,'succeeded',now(),NULL)
                ON CONFLICT(index_name,document_id) DO UPDATE SET content_hash=EXCLUDED.content_hash,version=EXCLUDED.version,
                status='succeeded',ingested_at=now(),failure_reason=NULL
                """;
            await using (var state = new NpgsqlCommand(stateSql, connection, transaction))
            {
                state.Parameters.AddWithValue("index", request.IndexName);
                state.Parameters.AddWithValue("document", request.DocumentId);
                state.Parameters.AddWithValue("hash", request.ContentHash);
                state.Parameters.AddWithValue("version", request.Version);
                await state.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new PostgreSqlRagDocumentUpsertResult
            {
                Status = existingHash is null ? PostgreSqlRagDocumentUpsertStatus.Created : PostgreSqlRagDocumentUpsertStatus.Updated,
                WrittenChunkCount = request.Records.Count,
            };
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<PostgreSqlRagDocumentDeleteResult> DeleteDocumentAsync(string indexName, string documentId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(indexName)) throw new ArgumentException("Index name is required.", nameof(indexName));
        if (string.IsNullOrWhiteSpace(documentId)) throw new ArgumentException("Document id is required.", nameof(documentId));
        await schema.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await LockAggregateAsync(connection, transaction, indexName, documentId, cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand($"DELETE FROM {schema.QuotedSchema}.rag_documents WHERE index_name=@index AND document_id=@document", connection, transaction);
        command.Parameters.AddWithValue("index", indexName);
        command.Parameters.AddWithValue("document", documentId);
        var count = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new PostgreSqlRagDocumentDeleteResult { Status = count == 0 ? PostgreSqlRagDocumentDeleteStatus.NotFound : PostgreSqlRagDocumentDeleteStatus.Deleted };
    }

    private async Task InsertChunksAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, PostgreSqlRagDocumentUpsertRequest request, CancellationToken cancellationToken)
    {
        await using var batch = new NpgsqlBatch(connection, transaction);
        foreach (var record in request.Records)
        {
            var command = new NpgsqlBatchCommand($"INSERT INTO {schema.QuotedSchema}.rag_chunks(index_name,document_id,chunk_id,content,chunk_order,token_count,metadata,embedding) VALUES(@index,@document,@chunk,@content,@order,@tokens,@metadata,@embedding)");
            command.Parameters.AddWithValue("index", request.IndexName);
            command.Parameters.AddWithValue("document", request.DocumentId);
            command.Parameters.AddWithValue("chunk", record.Id);
            command.Parameters.AddWithValue("content", record.Content);
            command.Parameters.AddWithValue("order", ParseInt(record.Metadata.Values, "chunkIndex") ?? 0);
            command.Parameters.AddWithValue("tokens", (object?)ParseInt(record.Metadata.Values, "tokenCount") ?? DBNull.Value);
            command.Parameters.Add(new NpgsqlParameter("metadata", NpgsqlDbType.Jsonb) { Value = Serialize(record.Metadata.Values) });
            command.Parameters.AddWithValue("embedding", new Vector(record.Values.ToArray()));
            batch.BatchCommands.Add(command);
        }
        if (batch.BatchCommands.Count > 0) await batch.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task LockAggregateAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string index, string document, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended(@key, 0))", connection, transaction);
        command.Parameters.AddWithValue("key", $"{schema.QuotedSchema}\u001f{index}\u001f{document}");
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<int?> ReadDimensionsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string index, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand($"SELECT embedding_dimension FROM {schema.QuotedSchema}.rag_indexes WHERE index_name=@index", connection, transaction);
        command.Parameters.AddWithValue("index", index);
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as int?;
    }

    private async Task<string?> ReadHashAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string index, string document, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand($"SELECT content_hash FROM {schema.QuotedSchema}.rag_documents WHERE index_name=@index AND document_id=@document", connection, transaction);
        command.Parameters.AddWithValue("index", index);
        command.Parameters.AddWithValue("document", document);
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
    }

    private static void Validate(PostgreSqlRagDocumentUpsertRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.IndexName)) throw new ArgumentException("Index name is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.DocumentId)) throw new ArgumentException("Document id is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.ContentHash)) throw new ArgumentException("Content hash is required.", nameof(request));
        if (request.Records.Any(record => record is null || string.IsNullOrWhiteSpace(record.Id) || record.Values.Count == 0)) throw new ArgumentException("Every chunk record must have an id and embedding.", nameof(request));
        if (request.Records.Select(record => record.Id).Distinct(StringComparer.Ordinal).Count() != request.Records.Count) throw new ArgumentException("Chunk ids must be unique within a document.", nameof(request));
    }

    private static int? ParseInt(IDictionary<string, string> values, string key) => values.TryGetValue(key, out var value) && int.TryParse(value, out var parsed) ? parsed : null;
    private static string Serialize(IDictionary<string, string> values) => JsonSerializer.Serialize(values);
}

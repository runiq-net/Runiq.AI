using Runiq.AI.Rag.Models.Metadata;
using Runiq.AI.Rag.Models.VectorStores;

namespace Runiq.AI.Rag.PostgreSql.Documents;

/// <summary>Identifies the observable outcome of a PostgreSQL document aggregate upsert.</summary>
public enum PostgreSqlRagDocumentUpsertStatus
{
    /// <summary>A new document aggregate was created.</summary>
    Created,
    /// <summary>An existing document aggregate was atomically replaced.</summary>
    Updated,
    /// <summary>The stored document already had the requested content hash and was left unchanged.</summary>
    Skipped,
}

/// <summary>Describes one document and its complete replacement chunk set.</summary>
public sealed class PostgreSqlRagDocumentUpsertRequest
{
    /// <summary>Gets or initializes the logical index name.</summary>
    public required string IndexName { get; init; }
    /// <summary>Gets or initializes the document identifier, unique within the logical index.</summary>
    public required string DocumentId { get; init; }
    /// <summary>Gets or initializes the stable hash of the source content.</summary>
    public required string ContentHash { get; init; }
    /// <summary>Gets or initializes the source descriptor.</summary>
    public string Source { get; init; } = string.Empty;
    /// <summary>Gets or initializes the document title.</summary>
    public string Title { get; init; } = string.Empty;
    /// <summary>Gets or initializes the application-defined document version.</summary>
    public string Version { get; init; } = string.Empty;
    /// <summary>Gets or initializes document metadata stored as JSONB.</summary>
    public RagMetadata Metadata { get; init; } = RagMetadata.Empty;
    /// <summary>Gets or initializes the complete chunk and embedding set for the document.</summary>
    public IReadOnlyList<VectorRecord> Records { get; init; } = [];
}

/// <summary>Reports the result of an atomic document aggregate upsert.</summary>
public sealed class PostgreSqlRagDocumentUpsertResult
{
    /// <summary>Gets the aggregate upsert outcome.</summary>
    public required PostgreSqlRagDocumentUpsertStatus Status { get; init; }
    /// <summary>Gets the number of chunks written; zero for an unchanged document.</summary>
    public int WrittenChunkCount { get; init; }
}

/// <summary>Identifies the observable outcome of a document aggregate delete.</summary>
public enum PostgreSqlRagDocumentDeleteStatus
{
    /// <summary>The document and its dependent records were deleted.</summary>
    Deleted,
    /// <summary>No matching document existed.</summary>
    NotFound,
}

/// <summary>Reports the result of a document aggregate delete.</summary>
public sealed class PostgreSqlRagDocumentDeleteResult
{
    /// <summary>Gets the delete outcome.</summary>
    public required PostgreSqlRagDocumentDeleteStatus Status { get; init; }
}

/// <summary>Persists complete RAG document aggregates in PostgreSQL.</summary>
public interface IPostgreSqlRagDocumentStore
{
    /// <summary>Creates, replaces, or skips a document aggregate atomically according to its content hash.</summary>
    /// <param name="request">The complete document aggregate.</param>
    /// <param name="cancellationToken">A token that cancels database work.</param>
    /// <returns>The structured aggregate outcome.</returns>
    Task<PostgreSqlRagDocumentUpsertResult> UpsertDocumentAsync(PostgreSqlRagDocumentUpsertRequest request, CancellationToken cancellationToken = default);

    /// <summary>Deletes one document aggregate within one logical index.</summary>
    /// <param name="indexName">The logical index name.</param>
    /// <param name="documentId">The document identifier.</param>
    /// <param name="cancellationToken">A token that cancels database work.</param>
    /// <returns>The structured delete outcome.</returns>
    Task<PostgreSqlRagDocumentDeleteResult> DeleteDocumentAsync(string indexName, string documentId, CancellationToken cancellationToken = default);
}

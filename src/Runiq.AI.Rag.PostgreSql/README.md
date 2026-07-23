# Runiq.AI.Rag.PostgreSql

The provider initializes idempotent lexical-search migrations alongside the existing pgvector schema.
Lexical retrieval uses a stored `tsvector` with a GIN index and a trigram GIN index over lower-cased original
content. The latter preserves identifier and punctuation matching for codes, symbols, namespaces, and file
names. Schema initialization requires permission to install `pg_trgm` in addition to the existing `vector`
extension.

This package adds durable PostgreSQL persistence and server-side pgvector search to `Runiq.AI.Rag`. Npgsql and
pgvector remain confined to this integration package; the core RAG package continues to work without it.

## Local setup

The repository compose file uses development-only credentials and a persistent volume:

```powershell
docker compose -f docker-compose.rag-postgresql.yml up -d
```

The connection string is `Host=localhost;Port=54329;Database=runiq_rag_dev;Username=runiq_dev;Password=runiq_dev_only`.
The volume keeps local data across container restarts; use `docker compose ... down -v` only when deliberate data
removal is wanted.

## Registration

```csharp
services.AddRuniqRag();
services.AddRuniqRagPostgreSql(options =>
{
    options.ConnectionString = connectionString;
    options.InitializeSchema = true;
    options.CreateVectorExtension = true; // explicit opt-in; normally provision this out of band
});
```

The last provider registration wins, consistently with the existing RAG vector-store convention. A missing
connection string fails during registration. Connection failures never fall back to memory.

For named index metadata, select the default or a named PostgreSQL store without opening a connection during index
registration:

```csharp
index.UsePostgreSqlVectorStore();
index.UsePostgreSqlVectorStore("corporate-store");
```

## Persistence and migrations

Versioned, provider-owned SQL persists logical indexes, documents, chunks, embeddings, JSONB metadata, and ingestion
state. `InitializeSchema` is opt-in, non-destructive, transactional, and idempotent. Extension creation requires the
separate `CreateVectorExtension` opt-in and suitable database privileges. Production environments should provision
the extension and apply reviewed migrations during deployment, then run with schema initialization disabled.

A logical index records the embedding model, dimension, and distance metric shared by its document aggregates. A
document aggregate consists of one document row, its ingestion-state row, and the complete set of chunks and pgvector
embeddings. Use `IPostgreSqlRagDocumentStore.UpsertDocumentAsync` to persist that aggregate:

```csharp
var outcome = await documentStore.UpsertDocumentAsync(new PostgreSqlRagDocumentUpsertRequest
{
    IndexName = "support",
    DocumentId = "handbook",
    ContentHash = contentHash,
    Version = "2026-07",
    Records = chunkVectors,
}, cancellationToken);
```

The operation takes a transaction-level advisory lock scoped to the index and document. A new hash creates the
aggregate. The same hash returns `Skipped` without rewriting chunks, embeddings, or ingestion state. A changed hash
updates document metadata, deletes the old chunk set, inserts the complete replacement set with one `NpgsqlBatch`,
and updates successful ingestion state in one transaction. Any failure rolls the whole replacement back. Use
`DeleteDocumentAsync(indexName, documentId)` for an index-scoped, idempotent delete; foreign-key cascades remove chunks,
their inline embeddings, and ingestion state. It returns `Deleted` or `NotFound` explicitly.

Every vector is checked against logical-index dimensions before document state is changed. Dimension mismatch throws
without partial writes. A failed transaction preserves the previous successful ingestion state; the provider does not
write a misleading successful or partially failed aggregate.

Chunk embeddings are stored in the pgvector `vector` type. Foreign keys cascade document deletion to chunks and
ingestion state, so no orphan embedding exists. Arbitrary metadata is JSONB with a GIN index. Writes use one open
connection and transaction per batch, with parameterized commands; a failed batch rolls back completely.

## Search semantics

Search runs in PostgreSQL (`ORDER BY embedding <operator> query`) and applies equality metadata criteria in the SQL
`WHERE` clause before the candidate limit. Ordering is distance first, then document id and chunk id. Cosine and
Euclidean raw values are lower-is-better distances; dot product is converted from pgvector's negative inner product
to a higher-is-better raw dot product. Cosine relevance is `1 - distance / 2`, Euclidean relevance is
`1 / (1 + distance)`, and unbounded dot product has no normalized relevance. The normal RAG retrieval and later
acceptance pipeline remain in control.

Exact scan is the safe default because one table may contain indexes with different dimensions and metrics. At
production scale, create dimension- and metric-specific partial HNSW indexes based on measured workloads; do not
assume the small integration-test data shape represents production.

Use the in-memory provider for tests, samples, and disposable local scenarios. Use PostgreSQL when logical index and
RAG records must survive process restarts and vector search must execute in the database. `IPostgreSqlRagHealthCheck`
reports connectivity, extension, schema, migration version, and index-table readability.

## Integration tests

Docker Desktop must be running with the Linux container engine. Start and wait for the real pgvector database, run
the integration collection, then stop it:

```powershell
docker compose -f docker-compose.rag-postgresql.yml up -d --wait
dotnet test tests/Runiq.AI.Rag.PostgreSql.Tests/Runiq.AI.Rag.PostgreSql.Tests.csproj --filter Category=Integration
docker compose -f docker-compose.rag-postgresql.yml down
```

Tests use deterministic vectors and a unique PostgreSQL schema per collection, and drop that schema after the run.
The persistent development volume is intentionally retained by `down`; add `-v` only for deliberate local cleanup.

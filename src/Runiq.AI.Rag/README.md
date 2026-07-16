# Runiq.AI.Rag

Runiq.AI.Rag provides retrieval-augmented generation primitives for Runiq AI applications.

It includes abstractions and default implementations for document chunking, embedding generation, vector storage, retrieval, and search result mapping.

The built-in in-memory store is intended for development and tests. Durable logical index, document, chunk,
embedding, metadata, and ingestion-state persistence with database-side vector search is available from the separate
`Runiq.AI.Rag.PostgreSql` package; the core package has no Npgsql or pgvector dependency.

## Retrieval Score Semantics

Retrieval results distinguish the provider's `RawScore` from nullable provider-independent `Relevance`.
`Metric` and `HigherIsBetter` define how the raw value must be interpreted; raw scores are never presented as
provider-independent confidence. Normalized relevance, when available, is always in the inclusive `[0,1]` range.

The in-memory adapter exposes cosine similarity as higher-is-better and normalizes it with `(raw + 1) / 2`. It
exposes Euclidean distance as lower-is-better and normalizes it with `1 / (1 + raw)`. Dot product is unbounded, so
the adapter reports its raw higher-is-better score with `Relevance = null` instead of inventing a normalization.
Provider adapters with a documented, reliable transformation may supply normalized relevance for their own metric.

`TopK` and `RagQuery.TopK` are candidate limits only. Agent context acceptance, duplicate filtering, relevance
thresholds, and maximum accepted-result limits are applied later by the single Agent runtime policy path described
in the [Agents package guide](../Runiq.AI.Agents/README.md#rag-execution-and-grounding-policies).

## Installation

```powershell
dotnet add package Runiq.AI.Rag --prerelease
```

## Related Packages

| Package | Purpose |
| --- | --- |
| `Runiq.AI.Agents` | Agent definitions, tool execution, provider integration, streaming events, and execution results. |
| `Runiq.AI.Core` | ASP.NET Core hosting extensions, runtime endpoints, and the embedded dashboard. |
| `Runiq.AI.Workflows` | Code-first workflow orchestration primitives for agent runtime and dashboard scenarios. |

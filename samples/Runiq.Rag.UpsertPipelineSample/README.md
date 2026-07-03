# Runiq RAG Vector Store Upsert Pipeline Sample

This console sample makes the `Document -> Chunk -> Embedding -> Vector Store upsert` flow visible from terminal output.

Run it from the repository root:

```powershell
dotnet run --project samples/Runiq.Rag.UpsertPipelineSample/Runiq.Rag.UpsertPipelineSample.csproj
```

The sample reads `samples/Runiq.Rag.UpsertPipelineSample/sample-document.txt` from disk, creates a `RagDocument`, runs the existing RAG dependency injection setup (`AddRuniqRag` with the in-memory vector store), chunks the document with the default chunker, generates embeddings with a deterministic fake provider, and writes the resulting vector records through the Vector Store upsert pipeline (`IRagVectorStoreUpsertPipeline`) under the explicit index name `sample-upsert-index`.

No API key is required. The sample has no network or database dependency and does not call any production embedding provider or vector database. It does not demonstrate retrieval or query behavior.

The terminal output shows the input file, document id, index name, generated chunk ids, chunk previews, embedding dimensions per chunk, and the upsert result including `Succeeded`, `AttemptedCount`, `ProcessedCount`, `FailedCount`, `ErrorCode`, and the upserted vector ids.

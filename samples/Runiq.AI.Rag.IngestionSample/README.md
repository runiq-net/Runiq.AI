# Runiq RAG Ingestion Sample

This console sample runs the unified `RagSourceDocument -> parse -> chunk -> embed -> vector-store persistence` pipeline with a deterministic local embedding provider and in-memory vector store.

Run it from the repository root:

```powershell
dotnet run --project samples/Runiq.AI.Rag.IngestionSample/Runiq.AI.Rag.IngestionSample.csproj
```

The sample reads `samples/Runiq.AI.Rag.IngestionSample/sample-document.txt` from disk and calls `rag.IngestAsync(document, "sample-documents")`. Run it again unchanged to see an idempotent skip; edit the text to create an update.

No API key is required. The sample has no network dependency and does not call OpenAI, Azure OpenAI, or any production embedding provider. It also does not write vectors to a vector store.

The terminal output shows the document id, generated chunk ids, chunk indexes, chunk previews, chunk metadata, embedding vector dimensions, and the chunk-to-embedding association.

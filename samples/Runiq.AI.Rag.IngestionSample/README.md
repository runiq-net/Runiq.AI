# Runiq RAG Ingestion Sample

This console sample makes the `Document -> Chunk -> Embedding` ingestion flow visible from terminal output.

Run it from the repository root:

```powershell
dotnet run --project samples/Runiq.AI.Rag.IngestionSample/Runiq.AI.Rag.IngestionSample.csproj
```

The sample reads `samples/Runiq.AI.Rag.IngestionSample/sample-document.txt` from disk, creates a `RagDocument`, runs the existing RAG dependency injection setup, chunks the document with the default chunker, and generates embeddings with a deterministic fake provider.

No API key is required. The sample has no network dependency and does not call OpenAI, Azure OpenAI, or any production embedding provider. It also does not write vectors to a vector store.

The terminal output shows the document id, generated chunk ids, chunk indexes, chunk previews, chunk metadata, embedding vector dimensions, and the chunk-to-embedding association.

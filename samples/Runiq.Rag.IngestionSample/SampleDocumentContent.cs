namespace Runiq.Rag.IngestionSample;

/// <summary>
/// Carries the sample document path and text loaded from disk.
/// </summary>
/// <param name="Path">The resolved sample document path.</param>
/// <param name="Content">The file content used to create the RAG document.</param>
public sealed record SampleDocumentContent(string Path, string Content);

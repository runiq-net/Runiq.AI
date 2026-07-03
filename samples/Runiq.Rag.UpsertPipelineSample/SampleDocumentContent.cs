namespace Runiq.Rag.UpsertPipelineSample;

/// <summary>
/// Carries the sample document path and text loaded from disk, so the console output can show
/// exactly which checked-in file was used as the upsert pipeline input.
/// </summary>
/// <param name="Path">The resolved sample document path.</param>
/// <param name="Content">The file content used to create the RAG document.</param>
public sealed record SampleDocumentContent(string Path, string Content);

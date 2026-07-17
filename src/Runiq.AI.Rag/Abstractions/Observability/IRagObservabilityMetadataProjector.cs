namespace Runiq.AI.Rag.Abstractions.Observability;

/// <summary>Projects application-approved string metadata into a safe RAG observability snapshot.</summary>
public interface IRagObservabilityMetadataProjector
{
    /// <summary>Returns explicitly approved metadata, or <see langword="null"/> to emit no metadata.</summary>
    /// <param name="metadata">The raw provider-independent string metadata.</param>
    /// <returns>A string-only safe metadata projection, or <see langword="null"/>.</returns>
    IReadOnlyDictionary<string, string>? Project(IReadOnlyDictionary<string, string> metadata);
}

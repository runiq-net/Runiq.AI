namespace Runiq.AI.Rag.Configuration;

/// <summary>Provides read-only access to registered RAG index definitions and safe metadata.</summary>
public interface IRagIndexRegistry
{
    /// <summary>Gets all registrations in deterministic registration order.</summary>
    IReadOnlyList<RagIndexRegistration> Registrations { get; }

    /// <summary>Gets provider-independent metadata suitable for hosting and dashboard projection.</summary>
    /// <returns>The safe metadata for all registered indexes.</returns>
    IReadOnlyList<RagIndexMetadata> GetMetadata();
}

/// <summary>Describes safe, provider-independent metadata for a registered RAG index.</summary>
public sealed class RagIndexMetadata
{
    internal RagIndexMetadata(RagIndexRegistration registration)
    {
        Name = registration.Name;
        Sources = registration.Sources.Select(source => new RagDocumentSourceMetadata(source.Identity, source.SourceType, source.DisplayValue)).ToArray();
        VectorStoreReference = registration.VectorStoreReference;
        EmbeddingReference = registration.EmbeddingReference;
        ChunkingSummary = $"max:{registration.Chunking.MaxChunkLength};overlap:{registration.Chunking.ChunkOverlap}";
        IngestionStrategyKind = registration.IngestionStrategy.Kind;
        ScheduleExpression = registration.IngestionStrategy.ScheduleExpression;
        EmbeddingDisplayName = registration.EmbeddingDisplayName;
        VectorStoreType = registration.VectorStoreType;
        VectorStoreDisplayName = registration.VectorStoreDisplayName;
        NamedVectorStoreReference = registration.NamedVectorStoreReference;
        ChunkSize = registration.Chunking.MaxChunkLength;
        ChunkOverlap = registration.Chunking.ChunkOverlap;
    }

    /// <summary>Gets the logical index name.</summary>
    public string Name { get; }
    /// <summary>Gets the number of registered sources.</summary>
    public int SourceCount => Sources.Count;
    /// <summary>Gets safe source metadata.</summary>
    public IReadOnlyList<RagDocumentSourceMetadata> Sources { get; }
    /// <summary>Gets the effective vector store reference.</summary>
    public string VectorStoreReference { get; }
    /// <summary>Gets the effective embedding reference.</summary>
    public string EmbeddingReference { get; }
    /// <summary>Gets a compact chunking configuration summary.</summary>
    public string ChunkingSummary { get; }
    /// <summary>Gets the configured ingestion lifecycle strategy; manual is the default.</summary>
    public RagIngestionStrategyKind IngestionStrategyKind { get; }
    /// <summary>Gets the scheduled expression for scheduled ingestion; otherwise, <see langword="null"/>.</summary>
    public string? ScheduleExpression { get; }
    /// <summary>Gets the safe embedding display name.</summary>
    public string EmbeddingDisplayName { get; }
    /// <summary>Gets the provider-independent vector-store type.</summary>
    public string VectorStoreType { get; }
    /// <summary>Gets the safe vector-store display name.</summary>
    public string VectorStoreDisplayName { get; }
    /// <summary>Gets the named vector-store reference, when present.</summary>
    public string? NamedVectorStoreReference { get; }
    /// <summary>Gets the configured maximum chunk size.</summary>
    public int ChunkSize { get; }
    /// <summary>Gets the configured overlap between adjacent chunks.</summary>
    public int ChunkOverlap { get; }
    /// <summary>Gets a value indicating whether startup validation accepted this definition.</summary>
    public bool IsValid => true;
}

/// <summary>Describes a document source without exposing credentials or full paths.</summary>
public sealed record RagDocumentSourceMetadata
{
    internal RagDocumentSourceMetadata(string identity, string sourceType, string displayValue) { Identity = identity; SourceType = sourceType; DisplayValue = displayValue; }
    /// <summary>Gets the stable safe source identity.</summary>
    public string Identity { get; }
    /// <summary>Gets the provider-independent source type.</summary>
    public string SourceType { get; }
    /// <summary>Gets the safe source display value.</summary>
    public string DisplayValue { get; }
}

internal sealed class RagIndexRegistry(IReadOnlyList<RagIndexRegistration> registrations) : IRagIndexRegistry
{
    public IReadOnlyList<RagIndexRegistration> Registrations { get; } = registrations;
    public IReadOnlyList<RagIndexMetadata> GetMetadata() => Registrations.Select(registration => new RagIndexMetadata(registration)).ToArray();
}

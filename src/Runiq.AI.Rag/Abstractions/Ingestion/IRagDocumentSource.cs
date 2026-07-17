using Runiq.AI.Rag.Models.Ingestion;

namespace Runiq.AI.Rag.Abstractions.Ingestion;

/// <summary>Discovers source documents and supplies their content for RAG ingestion.</summary>
public interface IRagDocumentSource
{
    /// <summary>Gets the stable identity used to distinguish this source within an index.</summary>
    string Identity => GetType().FullName ?? GetType().Name;

    /// <summary>Gets the provider-independent source type displayed by registry consumers.</summary>
    string SourceType => GetType().Name;

    /// <summary>Gets a safe display value that does not expose source credentials or full paths.</summary>
    string DisplayValue => SourceType;

    /// <summary>Gets the documents exposed by this source in deterministic order.</summary>
    /// <param name="cancellationToken">A token that can cancel discovery.</param>
    /// <returns>The source documents.</returns>
    Task<IReadOnlyList<RagSourceDocument>> GetDocumentsAsync(CancellationToken cancellationToken = default);
}

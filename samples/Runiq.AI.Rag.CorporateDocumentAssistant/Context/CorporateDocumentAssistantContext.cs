using Runiq.AI.ContextSpaces.Models.Sources;

namespace Runiq.AI.Rag.CorporateDocumentAssistant.Context;

/// <summary>
/// Creates the context space that exposes the sample corporate documents in the embedded dashboard.
/// </summary>
internal static class CorporateDocumentAssistantContext
{
    /// <summary>
    /// Creates the corporate document context space used by the sample host.
    /// </summary>
    /// <returns>A context space backed by the checked-in sample document folder.</returns>
    public static ContextSpace Create()
    {
        return new ContextSpace(
                id: "corporate-documents",
                name: "Corporate Documents",
                description: "Internal IT support procedures and security guidance used by the RAG sample.")
            .AddSources(sources => sources.FromFileSystem(
                id: "corporate-document-files",
                name: "Corporate Document Files",
                path: Path.Join(AppContext.BaseDirectory, "SampleDocuments"),
                description: "Markdown source documents for the Corporate Document Assistant sample."));
    }
}


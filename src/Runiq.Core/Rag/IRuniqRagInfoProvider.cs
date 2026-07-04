namespace Runiq.Core.Rag;

/// <summary>
/// Provides read-only visibility information for the configured Runiq RAG services.
/// </summary>
public interface IRuniqRagInfoProvider
{
    /// <summary>
    /// Gets the current RAG configuration visibility information without executing any RAG operation.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The current RAG configuration information.</returns>
    Task<RuniqRagInfo> GetInfoAsync(
        CancellationToken cancellationToken = default);
}

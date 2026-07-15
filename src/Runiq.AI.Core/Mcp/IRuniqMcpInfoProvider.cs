namespace Runiq.AI.Core.Mcp;

/// <summary>
/// Provides read-only visibility information for the configured Runiq MCP server.
/// </summary>
public interface IRuniqMcpInfoProvider
{
    /// <summary>
    /// Gets the current MCP server visibility information.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The current MCP server information.</returns>
    Task<RuniqMcpInfo> GetInfoAsync(
        CancellationToken cancellationToken = default);
}


namespace Runiq.Core.Mcp;

/// <summary>
/// Describes the MCP server endpoint and exposed MCP tools for dashboard display.
/// </summary>
public sealed class RuniqMcpInfo
{
    /// <summary>
    /// Gets a value indicating whether an MCP server endpoint was detected.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Gets the mapped MCP server endpoint path.
    /// </summary>
    public string? Endpoint { get; init; }

    /// <summary>
    /// Gets the absolute MCP server URL derived from the current request.
    /// </summary>
    public string? FullUrl { get; init; }

    /// <summary>
    /// Gets the configured MCP transport label.
    /// </summary>
    public string Transport { get; init; } = "Streamable HTTP";

    /// <summary>
    /// Gets a value indicating whether the MCP HTTP transport is stateless.
    /// </summary>
    public bool Stateless { get; init; } = true;

    /// <summary>
    /// Gets the MCP endpoint authentication label.
    /// </summary>
    public string Authentication { get; init; } = "None";

    /// <summary>
    /// Gets the MCP tools detected from loaded assemblies.
    /// </summary>
    public IReadOnlyList<RuniqMcpToolInfo> Tools { get; init; } = [];
}

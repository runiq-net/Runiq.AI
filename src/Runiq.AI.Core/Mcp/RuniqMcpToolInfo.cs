namespace Runiq.AI.Core.Mcp;

/// <summary>
/// Describes a single MCP tool exposed by the application.
/// </summary>
public sealed class RuniqMcpToolInfo
{
    /// <summary>
    /// Gets the MCP tool name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the MCP tool description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the source category for the MCP tool.
    /// </summary>
    public string Source { get; init; } = "MCP";

    /// <summary>
    /// Gets a value indicating whether the MCP tool requires input.
    /// </summary>
    public bool HasInput { get; init; }

    /// <summary>
    /// Gets the JSON schema-like input metadata used by the dashboard form.
    /// </summary>
    public IReadOnlyDictionary<string, object?> InputSchema { get; init; } =
        new Dictionary<string, object?>();
}


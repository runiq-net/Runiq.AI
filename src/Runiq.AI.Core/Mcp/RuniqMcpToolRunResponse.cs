namespace Runiq.AI.Core.Mcp;

/// <summary>
/// Represents the result of a dashboard MCP tool run.
/// </summary>
public sealed record RuniqMcpToolRunResponse(
    bool IsSuccess,
    string? OutputJson,
    string? ErrorCode,
    string? ErrorMessage);


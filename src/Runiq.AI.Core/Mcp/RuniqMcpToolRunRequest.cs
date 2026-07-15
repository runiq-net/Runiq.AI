using System.Text.Json;

namespace Runiq.AI.Core.Mcp;

/// <summary>
/// Represents a dashboard request to run an MCP tool.
/// </summary>
public sealed record RuniqMcpToolRunRequest(
    JsonElement? Input);


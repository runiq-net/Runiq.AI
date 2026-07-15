using System.Reflection;

namespace Runiq.AI.Core.Mcp;

internal sealed record RuniqMcpToolDescriptor(
    Type ToolType,
    MethodInfo Method,
    string Name,
    string? Description,
    IReadOnlyDictionary<string, object?> InputSchema,
    bool HasInput);


namespace Runiq.Core.Metadata;

/// <summary>
/// Studio tarafına dönen agent metadata bilgisini temsil eder.
/// </summary>
public sealed record AgentMetadataDto(
    string Id,
    string Name,
    string Instructions,
    string Model,
    string ReasoningEffort,
    string Verbosity,
    IReadOnlyList<AgentToolMetadataDto> Tools);

/// <summary>
/// Studio tarafında gösterilecek agent tool metadata bilgisini temsil eder.
/// </summary>
public sealed record AgentToolMetadataDto(
    string Name,
    string Description,
    string InputType,
    string OutputType);

/// <summary>
/// Studio tarafında gösterilecek sistem geneli tool metadata bilgisini temsil eder.
/// </summary>
public sealed record ToolMetadataDto(
    string Name,
    string DisplayName,
    string Description,
    string TypeName,
    string InputType,
    string OutputType,
    bool HasInput,
    IReadOnlyDictionary<string, object?> InputSchema,
    IReadOnlyDictionary<string, object?> OutputSchema,
    IReadOnlyList<ToolAttachedAgentMetadataDto> AttachedAgents);

/// <summary>
/// Bir tool'un bağlı olduğu agent bilgisini temsil eder.
/// </summary>
public sealed record ToolAttachedAgentMetadataDto(
    string Id,
    string Name);
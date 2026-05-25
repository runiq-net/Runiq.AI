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
    IReadOnlyList<AgentToolMetadataDto> Tools,
    IReadOnlyList<AgentContextSpaceMetadataDto> ContextSpaces);

/// <summary>
/// Studio tarafında agent'a bağlı context space metadata bilgisini temsil eder.
/// </summary>
public sealed record AgentContextSpaceMetadataDto(
    string Id,
    string Name,
    string? Description,
    int SourceCount,
    int DocumentCount,
    int SkillCount);

/// <summary>
/// Studio tarafında gösterilecek agent tool metadata bilgisini temsil eder.
/// </summary>
public sealed record AgentToolMetadataDto(
    string Name,
    string DisplayName,
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

/// <summary>
/// Studio tarafında gösterilecek context space metadata bilgisini temsil eder.
/// </summary>
public sealed record ContextSpaceMetadataDto(
    string Id,
    string Name,
    string? Description,
    IReadOnlyList<ContextSpaceSourceMetadataDto> Sources,
    IReadOnlyList<ContextSpaceSkillSourceMetadataDto> SkillSources,
    IReadOnlyList<ContextSpaceSkillMetadataDto> Skills,
    IReadOnlyList<ContextSpaceAttachedAgentMetadataDto> AttachedAgents);

/// <summary>
/// Context space içinde tanımlı skill kaynağı metadata bilgisini temsil eder.
/// </summary>
public sealed record ContextSpaceSkillSourceMetadataDto(
    string Id,
    string Name,
    string Kind,
    string? Path,
    string? BucketName,
    string? Prefix);

/// <summary>
/// Context space içinde keşfedilen skill metadata bilgisini temsil eder.
/// </summary>
public sealed record ContextSpaceSkillMetadataDto(
    string Id,
    string Name,
    string? Description,
    string? Version,
    IReadOnlyList<string> Tags,
    string SourceId,
    string RelativePath);

/// <summary>
/// Context space içinde tanımlı bilgi kaynağı metadata bilgisini temsil eder.
/// </summary>
public sealed record ContextSpaceSourceMetadataDto(
    string Id,
    string Name,
    string Kind,
    string? Description,
    string? Path,
    string? BucketName,
    string? Prefix);

/// <summary>
/// Bir context space'e bağlı agent bilgisini temsil eder.
/// </summary>
public sealed record ContextSpaceAttachedAgentMetadataDto(
    string Id,
    string Name);

namespace Runiq.Teams.Models.Execution;

/// <summary>
/// Agent team yürütmesi sırasında oluşan tek bir runtime event bilgisini taşır.
/// </summary>
public sealed record TeamExecutionEvent(
    TeamExecutionEventType Type,
    string TeamId,
    string? TeamName = null,
    string? MemberAgentId = null,
    string? MemberRole = null,
    string? Content = null,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    string? ToolCallId = null,
    string? ToolName = null,
    string? ArgumentsJson = null,
    string? OutputJson = null,
    bool IsFinalMember = false)
{
    /// <summary>
    /// Takım yürütmesi başladı event'i oluşturur.
    /// </summary>
    public static TeamExecutionEvent TeamStarted(
        string teamId,
        string teamName)
    {
        return new TeamExecutionEvent(
            Type: TeamExecutionEventType.TeamStarted,
            TeamId: NormalizeRequired(teamId, nameof(teamId)),
            TeamName: NormalizeRequired(teamName, nameof(teamName)));
    }

    /// <summary>
    /// Takım üyesi yürütmesi başladı event'i oluşturur.
    /// </summary>
    public static TeamExecutionEvent MemberStarted(
        string teamId,
        string memberAgentId,
        string memberRole)
    {
        return new TeamExecutionEvent(
            Type: TeamExecutionEventType.MemberStarted,
            TeamId: NormalizeRequired(teamId, nameof(teamId)),
            MemberAgentId: NormalizeRequired(memberAgentId, nameof(memberAgentId)),
            MemberRole: NormalizeRequired(memberRole, nameof(memberRole)));
    }

    /// <summary>
    /// Takım üyesinden gelen kısmi cevap event'i oluşturur.
    /// </summary>
    public static TeamExecutionEvent MemberDelta(
        string teamId,
        string memberAgentId,
        string memberRole,
        string content,
        bool isFinalMember = false)
    {
        return new TeamExecutionEvent(
            Type: TeamExecutionEventType.MemberDelta,
            TeamId: NormalizeRequired(teamId, nameof(teamId)),
            MemberAgentId: NormalizeRequired(memberAgentId, nameof(memberAgentId)),
            MemberRole: NormalizeRequired(memberRole, nameof(memberRole)),
            Content: NormalizeDeltaContent(content, nameof(content)),
            IsFinalMember: isFinalMember);
    }

    /// <summary>
    /// Takım üyesi tarafından başlatılan tool çağrısı event'ini oluşturur.
    /// </summary>
    public static TeamExecutionEvent MemberToolCallStarted(
        string teamId,
        string memberAgentId,
        string memberRole,
        string toolCallId,
        string toolName,
        string? argumentsJson)
    {
        return new TeamExecutionEvent(
            Type: TeamExecutionEventType.MemberToolCallStarted,
            TeamId: NormalizeRequired(teamId, nameof(teamId)),
            MemberAgentId: NormalizeRequired(memberAgentId, nameof(memberAgentId)),
            MemberRole: NormalizeRequired(memberRole, nameof(memberRole)),
            ToolCallId: NormalizeRequired(toolCallId, nameof(toolCallId)),
            ToolName: NormalizeRequired(toolName, nameof(toolName)),
            ArgumentsJson: NormalizeOptional(argumentsJson));
    }

    /// <summary>
    /// Takım üyesi tarafından tamamlanan tool çağrısı event'ini oluşturur.
    /// </summary>
    public static TeamExecutionEvent MemberToolCallCompleted(
        string teamId,
        string memberAgentId,
        string memberRole,
        string toolCallId,
        string toolName,
        string? outputJson)
    {
        return new TeamExecutionEvent(
            Type: TeamExecutionEventType.MemberToolCallCompleted,
            TeamId: NormalizeRequired(teamId, nameof(teamId)),
            MemberAgentId: NormalizeRequired(memberAgentId, nameof(memberAgentId)),
            MemberRole: NormalizeRequired(memberRole, nameof(memberRole)),
            ToolCallId: NormalizeRequired(toolCallId, nameof(toolCallId)),
            ToolName: NormalizeRequired(toolName, nameof(toolName)),
            OutputJson: NormalizeOptional(outputJson));
    }

    /// <summary>
    /// Takım üyesi tarafından hata alan tool çağrısı event'ini oluşturur.
    /// </summary>
    public static TeamExecutionEvent MemberToolCallFailed(
        string teamId,
        string memberAgentId,
        string memberRole,
        string toolCallId,
        string toolName,
        string errorMessage,
        string? errorCode = null)
    {
        return new TeamExecutionEvent(
            Type: TeamExecutionEventType.MemberToolCallFailed,
            TeamId: NormalizeRequired(teamId, nameof(teamId)),
            MemberAgentId: NormalizeRequired(memberAgentId, nameof(memberAgentId)),
            MemberRole: NormalizeRequired(memberRole, nameof(memberRole)),
            ToolCallId: NormalizeRequired(toolCallId, nameof(toolCallId)),
            ToolName: NormalizeRequired(toolName, nameof(toolName)),
            ErrorCode: NormalizeOptional(errorCode),
            ErrorMessage: NormalizeRequired(errorMessage, nameof(errorMessage)));
    }

    /// <summary>
    /// Takım üyesi yürütmesi tamamlandı event'i oluşturur.
    /// </summary>
    public static TeamExecutionEvent MemberCompleted(
        string teamId,
        string memberAgentId,
        string memberRole,
        string? content = null)
    {
        return new TeamExecutionEvent(
            Type: TeamExecutionEventType.MemberCompleted,
            TeamId: NormalizeRequired(teamId, nameof(teamId)),
            MemberAgentId: NormalizeRequired(memberAgentId, nameof(memberAgentId)),
            MemberRole: NormalizeRequired(memberRole, nameof(memberRole)),
            Content: NormalizeOptional(content));
    }

    /// <summary>
    /// Takım üyesi yürütmesi hata aldı event'i oluşturur.
    /// </summary>
    public static TeamExecutionEvent MemberFailed(
        string teamId,
        string memberAgentId,
        string memberRole,
        string errorMessage,
        string? errorCode = null)
    {
        return new TeamExecutionEvent(
            Type: TeamExecutionEventType.MemberFailed,
            TeamId: NormalizeRequired(teamId, nameof(teamId)),
            MemberAgentId: NormalizeRequired(memberAgentId, nameof(memberAgentId)),
            MemberRole: NormalizeRequired(memberRole, nameof(memberRole)),
            ErrorCode: NormalizeOptional(errorCode),
            ErrorMessage: NormalizeRequired(errorMessage, nameof(errorMessage)));
    }

    /// <summary>
    /// Takım yürütmesi tamamlandı event'i oluşturur.
    /// </summary>
    public static TeamExecutionEvent TeamCompleted(
        string teamId,
        string? content = null)
    {
        return new TeamExecutionEvent(
            Type: TeamExecutionEventType.TeamCompleted,
            TeamId: NormalizeRequired(teamId, nameof(teamId)),
            Content: NormalizeOptional(content));
    }

    /// <summary>
    /// Takım yürütmesi hata aldı event'i oluşturur.
    /// </summary>
    public static TeamExecutionEvent TeamFailed(
        string teamId,
        string errorMessage,
        string? errorCode = null)
    {
        return new TeamExecutionEvent(
            Type: TeamExecutionEventType.TeamFailed,
            TeamId: NormalizeRequired(teamId, nameof(teamId)),
            ErrorCode: NormalizeOptional(errorCode),
            ErrorMessage: NormalizeRequired(errorMessage, nameof(errorMessage)));
    }

    private static string NormalizeRequired(
        string value,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                $"{parameterName} cannot be empty.",
                parameterName);
        }

        return value.Trim();
    }

    private static string NormalizeDeltaContent(
        string value,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);

        if (value.Length == 0)
        {
            throw new ArgumentException(
                $"{parameterName} cannot be empty.",
                parameterName);
        }

        return value;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}

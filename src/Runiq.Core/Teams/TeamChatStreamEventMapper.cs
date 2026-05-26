using Runiq.Teams.Models.Execution;

namespace Runiq.Core.Teams;

/// <summary>
/// Team execution olaylarını Dashboard'un beklediği stream DTO formatına çevirir.
/// </summary>
internal static class TeamChatStreamEventMapper
{
    public static TeamChatStreamEvent FromExecutionEvent(
        TeamExecutionEvent executionEvent)
    {
        ArgumentNullException.ThrowIfNull(executionEvent);

        return executionEvent.Type switch
        {
            TeamExecutionEventType.TeamStarted => new TeamChatStreamEvent(
                Type: "team_started",
                Content: null,
                TeamId: executionEvent.TeamId,
                TeamName: executionEvent.TeamName),

            TeamExecutionEventType.MemberStarted => new TeamChatStreamEvent(
                Type: "member_started",
                Content: null,
                TeamId: executionEvent.TeamId,
                MemberAgentId: executionEvent.MemberAgentId,
                MemberRole: executionEvent.MemberRole),

            TeamExecutionEventType.MemberDelta => new TeamChatStreamEvent(
                Type: "member_delta",
                Content: executionEvent.Content,
                TeamId: executionEvent.TeamId,
                MemberAgentId: executionEvent.MemberAgentId,
                MemberRole: executionEvent.MemberRole),

            TeamExecutionEventType.MemberCompleted => new TeamChatStreamEvent(
                Type: "member_completed",
                Content: executionEvent.Content,
                TeamId: executionEvent.TeamId,
                MemberAgentId: executionEvent.MemberAgentId,
                MemberRole: executionEvent.MemberRole),

            TeamExecutionEventType.MemberFailed => new TeamChatStreamEvent(
                Type: "member_failed",
                Content: executionEvent.Content,
                TeamId: executionEvent.TeamId,
                MemberAgentId: executionEvent.MemberAgentId,
                MemberRole: executionEvent.MemberRole,
                ErrorCode: executionEvent.ErrorCode,
                ErrorMessage: executionEvent.ErrorMessage),

            TeamExecutionEventType.TeamCompleted => new TeamChatStreamEvent(
                Type: "team_completed",
                Content: executionEvent.Content,
                TeamId: executionEvent.TeamId),

            TeamExecutionEventType.TeamFailed => new TeamChatStreamEvent(
                Type: "team_failed",
                Content: executionEvent.Content,
                TeamId: executionEvent.TeamId,
                ErrorCode: executionEvent.ErrorCode,
                ErrorMessage: executionEvent.ErrorMessage),

            _ => new TeamChatStreamEvent(
                Type: "team_failed",
                Content: $"Unsupported team execution event type: {executionEvent.Type}.",
                TeamId: executionEvent.TeamId,
                ErrorMessage: $"Unsupported team execution event type: {executionEvent.Type}.")
        };
    }
}
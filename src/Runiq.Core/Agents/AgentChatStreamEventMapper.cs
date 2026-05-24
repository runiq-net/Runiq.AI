using Runiq.Agents;

namespace Runiq.Core.Agents;

/// <summary>
/// Agent execution olaylarını Dashboard'un beklediği stream DTO formatına çevirir.
/// </summary>
internal static class AgentChatStreamEventMapper
{
    public static AgentChatStreamEvent FromExecutionEvent(AgentExecutionEvent executionEvent)
    {
        ArgumentNullException.ThrowIfNull(executionEvent);

        return executionEvent.Kind switch
        {
            AgentExecutionEventKind.AssistantDelta => new AgentChatStreamEvent(
                Type: "assistant_delta",
                Content: executionEvent.Content),

            AgentExecutionEventKind.ToolCallStarted => new AgentChatStreamEvent(
                Type: "tool_call_started",
                Content: executionEvent.Content,
                ToolCallId: executionEvent.ToolCallId,
                ToolName: executionEvent.ToolName,
                ArgumentsJson: executionEvent.ArgumentsJson),

            AgentExecutionEventKind.ToolCallCompleted => new AgentChatStreamEvent(
                Type: "tool_call_completed",
                Content: executionEvent.Content,
                ToolCallId: executionEvent.ToolCallId,
                ToolName: executionEvent.ToolName,
                OutputJson: executionEvent.OutputJson),

            AgentExecutionEventKind.ToolCallFailed => new AgentChatStreamEvent(
                Type: "tool_call_failed",
                Content: executionEvent.Content,
                ToolCallId: executionEvent.ToolCallId,
                ToolName: executionEvent.ToolName,
                ErrorCode: executionEvent.ErrorCode,
                ErrorMessage: executionEvent.ErrorMessage),

            AgentExecutionEventKind.ContextProvided => new AgentChatStreamEvent(
Type: "context_provided",
Content: null,
ContextSpaces: executionEvent.ContextSpaces?
    .Select(contextSpace => new AgentChatContextSpaceStreamItem(
        Id: contextSpace.Id,
        Name: contextSpace.Name,
        Description: contextSpace.Description))
    .ToArray(),
Skills: executionEvent.Skills?
    .Select(skill => new AgentChatSkillStreamItem(
        Id: skill.Id,
        Name: skill.Name,
        Description: skill.Description,
        Version: skill.Version,
        Tags: skill.Tags,
        SourceId: skill.SourceId,
        RelativePath: skill.RelativePath))
    .ToArray(),
Sources: executionEvent.Sources?
    .Select(source => new AgentChatSourceStreamItem(
        Id: source.Id,
        Name: source.Name,
        Kind: source.Kind,
        Description: source.Description))
    .ToArray()),

            AgentExecutionEventKind.Completed => new AgentChatStreamEvent(
                Type: "completed",
                Content: null),

            AgentExecutionEventKind.Failed => new AgentChatStreamEvent(
                Type: "failed",
                Content: executionEvent.Content,
                ErrorCode: executionEvent.ErrorCode,
                ErrorMessage: executionEvent.ErrorMessage),

            _ => new AgentChatStreamEvent(
                Type: "failed",
                Content: $"Unsupported agent execution event kind: {executionEvent.Kind}.",
                ErrorMessage: $"Unsupported agent execution event kind: {executionEvent.Kind}.")
        };
    }
}
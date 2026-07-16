using System.Text;

namespace Runiq.AI.Agents;

/// <summary>
/// Stream olarak gelen agent execution event'lerinden tamamlanmis AgentExecutionResult üretir.
/// </summary>
public sealed class AgentExecutionResultBuilder
{
    private readonly StringBuilder messageBuilder = new();
    private readonly List<AgentExecutionStep> steps = [];
    private readonly Dictionary<string, int> toolStepIndexesByCallId = new(StringComparer.OrdinalIgnoreCase);

    private int nextIndex = 1;
    private bool finalAnswerAdded;
    private string? failureCode;
    private string? failureMessage;
    private AgentRagExecutionMetadata? rag;

    /// <summary>
    /// Tek bir execution event'ini result state'ine uygular.
    /// </summary>
    public void Apply(AgentExecutionEvent executionEvent)
    {
        ArgumentNullException.ThrowIfNull(executionEvent);
        rag = executionEvent.Rag ?? rag;

        switch (executionEvent.Kind)
        {
            case AgentExecutionEventKind.AssistantDelta:
                AppendAssistantDelta(executionEvent);
                break;

            case AgentExecutionEventKind.ToolCallStarted:
                AddToolCallStep(executionEvent);
                break;

            case AgentExecutionEventKind.ToolCallCompleted:
                CompleteToolCallStep(executionEvent);
                break;

            case AgentExecutionEventKind.ToolCallFailed:
                FailToolCallStep(executionEvent);
                break;

            case AgentExecutionEventKind.Completed:
                AddFinalAnswerStep();
                break;

            case AgentExecutionEventKind.Failed:
                AddFailureStep(executionEvent);
                break;
        }
    }

    /// <summary>
    /// Toplanan event'lerden tamamlanmis agent execution result üretir.
    /// </summary>
    public AgentExecutionResult Build()
    {
        AddFinalAnswerStep();

        return failureCode is null
            ? AgentExecutionResult.Success(messageBuilder.ToString(), steps, rag)
            : AgentExecutionResult.Failure(
                failureCode,
                failureMessage ?? "Agent execution failed.",
                steps,
                rag);
    }

    private void AppendAssistantDelta(AgentExecutionEvent executionEvent)
    {
        if (!string.IsNullOrEmpty(executionEvent.Content))
        {
            messageBuilder.Append(executionEvent.Content);
        }
    }

    private void AddToolCallStep(AgentExecutionEvent executionEvent)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var step = new AgentExecutionStep(
            Index: nextIndex++,
            Kind: AgentExecutionStepKind.ToolCall,
            Content: null,
            ToolCallId: executionEvent.ToolCallId,
            ToolName: executionEvent.ToolName,
            ArgumentsJson: executionEvent.ArgumentsJson,
            OutputJson: null,
            ErrorCode: null,
            ErrorMessage: null,
            Status: AgentExecutionStepStatus.Running,
            StartedAt: startedAt,
            CompletedAt: null);

        steps.Add(step);

        if (!string.IsNullOrWhiteSpace(executionEvent.ToolCallId))
        {
            toolStepIndexesByCallId[executionEvent.ToolCallId] = steps.Count - 1;
        }
    }

    private void CompleteToolCallStep(AgentExecutionEvent executionEvent)
    {
        var stepIndex = FindToolStepIndex(executionEvent);

        if (stepIndex is null)
        {
            AddCompletedToolCallWithoutStart(executionEvent);
            return;
        }

        var existing = steps[stepIndex.Value];

        steps[stepIndex.Value] = existing with
        {
            OutputJson = executionEvent.OutputJson,
            Status = AgentExecutionStepStatus.Completed,
            CompletedAt = DateTimeOffset.UtcNow
        };
    }

    private void FailToolCallStep(AgentExecutionEvent executionEvent)
    {
        var stepIndex = FindToolStepIndex(executionEvent);

        if (stepIndex is null)
        {
            AddFailedToolCallWithoutStart(executionEvent);
            return;
        }

        var existing = steps[stepIndex.Value];

        steps[stepIndex.Value] = existing with
        {
            Content = executionEvent.Content,
            ErrorCode = executionEvent.ErrorCode,
            ErrorMessage = executionEvent.ErrorMessage,
            Status = AgentExecutionStepStatus.Failed,
            CompletedAt = DateTimeOffset.UtcNow
        };
    }

    private int? FindToolStepIndex(AgentExecutionEvent executionEvent)
    {
        if (string.IsNullOrWhiteSpace(executionEvent.ToolCallId))
        {
            return null;
        }

        return toolStepIndexesByCallId.TryGetValue(
            executionEvent.ToolCallId,
            out var stepIndex)
            ? stepIndex
            : null;
    }

    private void AddCompletedToolCallWithoutStart(AgentExecutionEvent executionEvent)
    {
        var now = DateTimeOffset.UtcNow;

        steps.Add(new AgentExecutionStep(
            Index: nextIndex++,
            Kind: AgentExecutionStepKind.ToolCall,
            Content: null,
            ToolCallId: executionEvent.ToolCallId,
            ToolName: executionEvent.ToolName,
            ArgumentsJson: executionEvent.ArgumentsJson,
            OutputJson: executionEvent.OutputJson,
            ErrorCode: null,
            ErrorMessage: null,
            Status: AgentExecutionStepStatus.Completed,
            StartedAt: now,
            CompletedAt: now));
    }

    private void AddFailedToolCallWithoutStart(AgentExecutionEvent executionEvent)
    {
        var now = DateTimeOffset.UtcNow;

        steps.Add(new AgentExecutionStep(
            Index: nextIndex++,
            Kind: AgentExecutionStepKind.ToolCall,
            Content: null,
            ToolCallId: executionEvent.ToolCallId,
            ToolName: executionEvent.ToolName,
            ArgumentsJson: executionEvent.ArgumentsJson,
            OutputJson: null,
            ErrorCode: executionEvent.ErrorCode,
            ErrorMessage: executionEvent.ErrorMessage,
            Status: AgentExecutionStepStatus.Failed,
            StartedAt: now,
            CompletedAt: now));
    }

    private void AddFinalAnswerStep()
    {
        if (finalAnswerAdded)
        {
            return;
        }

        var message = messageBuilder.ToString();

        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;

        steps.Add(new AgentExecutionStep(
            Index: nextIndex++,
            Kind: AgentExecutionStepKind.FinalAnswer,
            Content: message,
            ToolCallId: null,
            ToolName: null,
            ArgumentsJson: null,
            OutputJson: null,
            ErrorCode: null,
            ErrorMessage: null,
            Status: AgentExecutionStepStatus.Completed,
            StartedAt: now,
            CompletedAt: now));

        finalAnswerAdded = true;
    }

    private void AddFailureStep(AgentExecutionEvent executionEvent)
    {
        failureCode = executionEvent.ErrorCode ?? "AgentExecutionFailed";
        failureMessage =
            executionEvent.ErrorMessage ??
            executionEvent.Content ??
            "Agent execution failed.";

        var now = DateTimeOffset.UtcNow;

        steps.Add(new AgentExecutionStep(
            Index: nextIndex++,
            Kind: AgentExecutionStepKind.Error,
           Content: null,
            ToolCallId: executionEvent.ToolCallId,
            ToolName: executionEvent.ToolName,
            ArgumentsJson: executionEvent.ArgumentsJson,
            OutputJson: executionEvent.OutputJson,
            ErrorCode: failureCode,
            ErrorMessage: failureMessage,
            Status: AgentExecutionStepStatus.Failed,
            StartedAt: now,
            CompletedAt: now));
    }
}

namespace Runiq.Workflows.Domain;

/// <summary>
/// Describes how a flow should proceed after a step failure.
/// </summary>
public enum FailureBehavior
{
    Stop = 0,
    Continue = 1,
    GoTo = 2
}

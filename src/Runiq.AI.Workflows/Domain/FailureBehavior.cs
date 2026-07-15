namespace Runiq.AI.Workflows.Domain;

/// <summary>
/// Describes how a flow should proceed after a step failure.
/// </summary>
public enum FailureBehavior
{
    /// <summary>
    /// Stops the entire flow execution when the step fails.
    /// </summary>
    Stop = 0,

    /// <summary>
    /// Continues to the next configured step despite the failure.
    /// </summary>
    Continue = 1,

    /// <summary>
    /// Jumps to a specific fallback step when the step fails.
    /// </summary>
    GoTo = 2
}


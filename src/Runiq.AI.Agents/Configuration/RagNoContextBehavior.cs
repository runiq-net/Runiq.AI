namespace Runiq.AI.Agents.Configuration;

/// <summary>
/// Defines how an agent responds when retrieval succeeds but produces no accepted context.
/// </summary>
public enum RagNoContextBehavior
{
    /// <summary>
    /// Continues with normal model execution without accepted retrieval context.
    /// </summary>
    AnswerNormally = 0,

    /// <summary>
    /// Completes successfully with a framework-owned not-found response without invoking the model.
    /// </summary>
    ReturnNotFound = 1,

    /// <summary>
    /// Fails agent execution without invoking the model.
    /// </summary>
    FailExecution = 2,
}

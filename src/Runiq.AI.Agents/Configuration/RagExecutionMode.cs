namespace Runiq.AI.Agents.Configuration;

/// <summary>
/// Defines how an agent responds when an otherwise valid RAG retrieval operation fails.
/// </summary>
public enum RagExecutionMode
{
    /// <summary>
    /// Stops execution before model invocation when retrieval fails.
    /// </summary>
    Required,

    /// <summary>
    /// Continues without retrieved context when retrieval infrastructure is present but retrieval fails.
    /// Invalid configuration and missing infrastructure still stop execution.
    /// </summary>
    Optional,
}

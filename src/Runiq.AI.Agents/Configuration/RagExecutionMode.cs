namespace Runiq.AI.Agents.Configuration;

/// <summary>
/// Defines how accepted retrieval context constrains an agent response.
/// </summary>
public enum RagExecutionMode
{
    /// <summary>
    /// Uses accepted retrieval context when available and otherwise permits normal agent behavior.
    /// </summary>
    Open = 0,

    /// <summary>
    /// Treats accepted documents as the primary information source and requires unsupported information
    /// and conflicting sources to be identified explicitly.
    /// </summary>
    Grounded = 1,

    /// <summary>
    /// Restricts the response to accepted retrieval context and prevents normal model answering when no
    /// accepted context is available.
    /// </summary>
    Required = 2,
}

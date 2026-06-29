namespace Runiq.Core.Agents;

/// <summary>
/// Studio üzerinden agent'a gönderilen chat isteğini temsil eder.
/// </summary>
public sealed record AgentChatRequest(
    string Message,
    AgentChatResponseMode ResponseMode = AgentChatResponseMode.Stream,
    string? IndexName = null);

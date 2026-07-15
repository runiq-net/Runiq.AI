namespace Runiq.AI.Core.Agents;

/// <summary>
/// Studio üzerinden agent'a gönderilen chat istegini temsil eder.
/// </summary>
public sealed record AgentChatRequest(
    string Message,
    AgentChatResponseMode ResponseMode = AgentChatResponseMode.Stream,
    string? IndexName = null);


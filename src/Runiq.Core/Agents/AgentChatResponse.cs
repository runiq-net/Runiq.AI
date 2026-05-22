namespace Runiq.Core.Agents;

/// <summary>
/// Studio üzerinden çalıştırılan agent chat cevabını temsil eder.
/// </summary>
public sealed record AgentChatResponse(
    bool IsSuccess,
    string? Message,
    string? ErrorCode,
    string? ErrorMessage);
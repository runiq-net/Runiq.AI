namespace Runiq.AI.Core.Tools;

/// <summary>
/// Dashboard üzerinden çalistirilan tool cevabini temsil eder.
/// </summary>
public sealed record ToolRunResponse(
    bool IsSuccess,
    string? OutputJson,
    string? ErrorCode,
    string? ErrorMessage);

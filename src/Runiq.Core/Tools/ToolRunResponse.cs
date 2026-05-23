namespace Runiq.Core.Tools;

/// <summary>
/// Dashboard üzerinden çalıştırılan tool cevabını temsil eder.
/// </summary>
public sealed record ToolRunResponse(
    bool IsSuccess,
    string? OutputJson,
    string? ErrorCode,
    string? ErrorMessage);
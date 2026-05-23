using System.Text.Json;

namespace Runiq.Core.Tools;

/// <summary>
/// Dashboard üzerinden doğrudan tool çalıştırma isteğini temsil eder.
/// </summary>
public sealed record ToolRunRequest(
    JsonElement? Input);
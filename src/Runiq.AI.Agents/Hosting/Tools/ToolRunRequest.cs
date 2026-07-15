using System.Text.Json;

namespace Runiq.AI.Core.Tools;

/// <summary>
/// Dashboard üzerinden dogrudan tool çalistirma istegini temsil eder.
/// </summary>
public sealed record ToolRunRequest(
    JsonElement? Input);

using Microsoft.AspNetCore.Http;
using Runiq.AI.Agents.Tools;
using System.Text.Json;

namespace Runiq.AI.Core.Tools;

/// <summary>
/// Dashboard tool playground API isteklerini isler.
/// </summary>
public sealed class ToolRunApiHandler
{
    private readonly IReadOnlyList<AgentToolRegistration> registeredTools;
    private readonly AgentToolInvoker toolInvoker;

    /// <summary>
    /// Tool run API handler örnegini olusturur.
    /// </summary>
    public ToolRunApiHandler(
        IReadOnlyList<AgentToolRegistration> registeredTools,
        AgentToolInvoker toolInvoker)
    {
        this.registeredTools = registeredTools;
        this.toolInvoker = toolInvoker;
    }

    /// <summary>
    /// Istenen tool'u verilen input JSON ile dogrudan çalistirir.
    /// </summary>
    public async Task<IResult> RunAsync(
        string toolName,
        ToolRunRequest? request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return Results.BadRequest(new ToolRunResponse(
                IsSuccess: false,
                OutputJson: null,
                ErrorCode: "ToolNameRequired",
                ErrorMessage: "Tool name is required."));
        }

        var tool = registeredTools.FirstOrDefault(candidate =>
            candidate.Name.Equals(toolName, StringComparison.OrdinalIgnoreCase));

        if (tool is null)
        {
            return Results.NotFound(new ToolRunResponse(
                IsSuccess: false,
                OutputJson: null,
                ErrorCode: "ToolNotFound",
                ErrorMessage: $"Tool '{toolName}' could not be found."));
        }

        var inputJson =
            request?.Input is { ValueKind: not JsonValueKind.Undefined and not JsonValueKind.Null } input
                ? input.GetRawText()
                : "{}";

        var result = await toolInvoker.InvokeAsync(
            tool,
            inputJson,
            cancellationToken);

        return Results.Ok(new ToolRunResponse(
            IsSuccess: result.IsSuccess,
            OutputJson: result.OutputJson,
            ErrorCode: result.ErrorCode,
            ErrorMessage: result.ErrorMessage));
    }
}

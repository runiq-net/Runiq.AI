using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Runiq.AI.Agents;
using Runiq.AI.Agents.Runtime;
using Runiq.AI.Agents.Tools;

namespace Runiq.AI.Core.Agents;

/// <summary>
/// Handles Dashboard agent chat API requests.
/// </summary>
public sealed class AgentChatApiHandler
{
    private static readonly JsonSerializerOptions StreamJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AgentExecutionRuntime agentRuntime;

    /// <summary>
    /// Creates an Agent Chat API handler.
    /// </summary>
    public AgentChatApiHandler(AgentExecutionRuntime agentRuntime)
    {
        this.agentRuntime = agentRuntime;
    }

    /// <summary>
    /// Handles an Agent Chat request as either a JSON result or an SSE stream.
    /// </summary>
    public async Task<IResult> ChatAsync(
        string agentId,
        AgentChatRequest request,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return Results.BadRequest(new AgentChatResponse(
                IsSuccess: false,
                Message: null,
                ErrorCode: "MessageRequired",
                ErrorMessage: "Message is required.",
                Steps: []));
        }

        if (request.ResponseMode == AgentChatResponseMode.Stream)
        {
            await WriteStreamAsync(
                httpContext,
                agentId,
                CreateAgentQuery(request),
                cancellationToken);

            return Results.Empty;
        }

        var execution = await ExecuteToResultAsync(
            httpContext,
            agentId,
            CreateAgentQuery(request),
            cancellationToken);

        return Results.Ok(ToChatResponse(execution.Result, execution.GroundingEvidence));
    }

    private async Task<(AgentExecutionResult Result, IReadOnlyList<AgentChatRagSearchEvent> GroundingEvidence)> ExecuteToResultAsync(
        HttpContext httpContext,
        string agentId,
        AgentQuery query,
        CancellationToken cancellationToken)
    {
        var resultBuilder = new AgentExecutionResultBuilder();
        var groundingEvidence = new List<AgentChatRagSearchEvent>();
        var toolInvoker = new AgentToolInvoker(httpContext.RequestServices);

        await foreach (var executionEvent in agentRuntime.ExecuteStreamAsync(
                           agentId,
                           query,
                           toolInvoker,
                           cancellationToken))
        {
            resultBuilder.Apply(executionEvent);
            if (executionEvent.RagSearch is not null &&
                AgentChatStreamEventMapper.FromExecutionEvent(executionEvent) is { Type: "rag_search_completed", RagSearch: { } projection } &&
                groundingEvidence.All(item => item.CorrelationId != projection.CorrelationId))
            {
                groundingEvidence.Add(projection);
            }
        }

        return (resultBuilder.Build(), groundingEvidence);
    }

    private static AgentChatResponse ToChatResponse(AgentExecutionResult result, IReadOnlyList<AgentChatRagSearchEvent> groundingEvidence)
    {
        return new AgentChatResponse(
            IsSuccess: result.IsSuccess,
            Message: result.Message,
            ErrorCode: result.ErrorCode,
            ErrorMessage: result.ErrorMessage,
            Steps: result.Steps
                .Select(AgentChatExecutionStepResponse.FromExecutionStep)
                .ToArray())
        {
            Rag = result.Rag,
            GroundingEvidence = groundingEvidence.Count == 0 ? null : groundingEvidence,
            Citations = result.Citations.Count == 0 ? null : result.Citations,
            RagReadiness = result.RagReadiness is null
                ? null
                : AgentChatStreamEventMapper.FromExecutionEvent(AgentExecutionEvent.FromRagSearch(result.RagReadiness)).RagSearch,
        };
    }

    private async Task WriteStreamAsync(
        HttpContext httpContext,
        string agentId,
        AgentQuery query,
        CancellationToken cancellationToken)
    {
        httpContext.Response.ContentType = "text/event-stream; charset=utf-8";
        httpContext.Response.Headers.CacheControl = "no-cache";
        httpContext.Response.Headers.Connection = "keep-alive";

        var toolInvoker = new AgentToolInvoker(httpContext.RequestServices);

        await foreach (var executionEvent in agentRuntime.ExecuteStreamAsync(
                           agentId,
                           query,
                           toolInvoker,
                           cancellationToken))
        {
            var streamEvent = AgentChatStreamEventMapper.FromExecutionEvent(executionEvent);
            var payload = JsonSerializer.Serialize(streamEvent, StreamJsonOptions);

            await httpContext.Response.WriteAsync($"data: {payload}\n\n", cancellationToken);
            await httpContext.Response.Body.FlushAsync(cancellationToken);
        }

        await httpContext.Response.WriteAsync("data: [DONE]\n\n", cancellationToken);
        await httpContext.Response.Body.FlushAsync(cancellationToken);
    }

    private static AgentQuery CreateAgentQuery(AgentChatRequest request)
    {
        return new AgentQuery(request.Message)
        {
            IndexName = request.IndexName,
        };
    }

}

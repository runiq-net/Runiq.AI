using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Runiq.AI.Agents;
using Runiq.AI.Agents.Tools;
using Runiq.AI.Agents.Runtime;


namespace Runiq.AI.Core.Agents;

/// <summary>
/// Dashboard agent chat API isteklerini isler.
/// </summary>
public sealed class AgentChatApiHandler
{
    private static readonly JsonSerializerOptions StreamJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AgentExecutionRuntime agentRuntime;

    /// <summary>
    /// Agent chat API handler örnegini olusturur.
    /// </summary>
    public AgentChatApiHandler(AgentExecutionRuntime agentRuntime)
    {
        this.agentRuntime = agentRuntime;
    }

    /// <summary>
    /// Agent chat istegini cevap moduna göre JSON sonuç veya SSE stream olarak isler.
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

        var result = await ExecuteToResultAsync(
            httpContext,
            agentId,
            CreateAgentQuery(request),
            cancellationToken);

        return Results.Ok(ToChatResponse(result));
    }

    private async Task<AgentExecutionResult> ExecuteToResultAsync(
        HttpContext httpContext,
        string agentId,
        AgentQuery query,
        CancellationToken cancellationToken)
    {
        var resultBuilder = new AgentExecutionResultBuilder();
        var toolInvoker = new AgentToolInvoker(httpContext.RequestServices);

        await foreach (var executionEvent in agentRuntime.ExecuteStreamAsync(
                           agentId,
                           query,
                           toolInvoker,
                           cancellationToken))
        {
            resultBuilder.Apply(executionEvent);
        }

        return resultBuilder.Build();
    }

    private static AgentChatResponse ToChatResponse(AgentExecutionResult result)
    {
        return new AgentChatResponse(
            IsSuccess: result.IsSuccess,
            Message: result.Message,
            ErrorCode: result.ErrorCode,
            ErrorMessage: result.ErrorMessage,
            Steps: result.Steps
                .Select(AgentChatExecutionStepResponse.FromExecutionStep)
                .ToArray());
    }

    private  async Task WriteStreamAsync(
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


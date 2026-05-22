using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Runiq.Agents;
using Runiq.Agents.Tools;
using Runiq.Agents.Runtime;

namespace Runiq.Core.Agents;

/// <summary>
/// Dashboard agent chat API isteklerini işler.
/// </summary>
public sealed class AgentChatApiHandler
{
    private static readonly JsonSerializerOptions StreamJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AgentExecutionRuntime agentRuntime;

    /// <summary>
    /// Agent chat API handler örneğini oluşturur.
    /// </summary>
    public AgentChatApiHandler(AgentExecutionRuntime agentRuntime)
    {
        this.agentRuntime = agentRuntime;
    }

    /// <summary>
    /// Agent chat isteğini cevap moduna göre JSON sonuç veya SSE stream olarak işler.
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
                ErrorMessage: "Message is required."));
        }

        if (request.ResponseMode == AgentChatResponseMode.Stream)
        {
            await WriteStreamAsync(
                httpContext,
                agentId,
                request.Message,
                cancellationToken);

            return Results.Empty;
        }

        var result = await ExecuteToResultAsync(
            httpContext,
            agentId,
            request.Message,
            cancellationToken);

        return Results.Ok(ToChatResponse(result));
    }

    private async Task<AgentExecutionResult> ExecuteToResultAsync(
    HttpContext httpContext,
    string agentId,
    string message,
    CancellationToken cancellationToken)
    {
        var builder = new System.Text.StringBuilder();
        var toolInvoker = new AgentToolInvoker(httpContext.RequestServices);

        await foreach (var executionEvent in agentRuntime.ExecuteStreamAsync(
                             agentId,
                            message,
                            toolInvoker,
                            cancellationToken))
        {
            switch (executionEvent.Kind)
            {
                case AgentExecutionEventKind.AssistantDelta:
                    if (!string.IsNullOrEmpty(executionEvent.Content))
                    {
                        builder.Append(executionEvent.Content);
                    }

                    break;

                case AgentExecutionEventKind.Failed:
                    return AgentExecutionResult.Failure(
                        errorCode: executionEvent.ErrorCode ?? "AgentExecutionFailed",
                        errorMessage:
                        executionEvent.ErrorMessage ??
                        executionEvent.Content ??
                        "Agent execution failed.");

                case AgentExecutionEventKind.Completed:
                    break;
            }
        }

        return AgentExecutionResult.Success(builder.ToString());
    }



    private static AgentChatResponse ToChatResponse(AgentExecutionResult result)
    {
        return new AgentChatResponse(
            IsSuccess: result.IsSuccess,
            Message: result.Message,
            ErrorCode: result.ErrorCode,
            ErrorMessage: result.ErrorMessage);
    }

    private  async Task WriteStreamAsync(
        HttpContext httpContext,
        string agentId,
        string message,
        CancellationToken cancellationToken)
    {
        httpContext.Response.ContentType = "text/event-stream; charset=utf-8";
        httpContext.Response.Headers.CacheControl = "no-cache";
        httpContext.Response.Headers.Connection = "keep-alive";

        var toolInvoker = new AgentToolInvoker(httpContext.RequestServices);

        await foreach (var executionEvent in agentRuntime.ExecuteStreamAsync(
                        agentId,
                        message,
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
}
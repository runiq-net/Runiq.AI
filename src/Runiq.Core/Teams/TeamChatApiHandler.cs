using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Runiq.Core.Agents;
using Runiq.Teams.Execution;

namespace Runiq.Core.Teams;

/// <summary>
/// Dashboard agent team chat API isteklerini işler.
/// </summary>
public sealed class TeamChatApiHandler
{
    private static readonly JsonSerializerOptions StreamJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly TeamExecutionRuntime teamRuntime;

    /// <summary>
    /// Agent team chat API handler örneğini oluşturur.
    /// </summary>
    public TeamChatApiHandler(TeamExecutionRuntime teamRuntime)
    {
        this.teamRuntime = teamRuntime;
    }

    /// <summary>
    /// Agent team chat isteğini SSE stream olarak işler.
    /// </summary>
    public async Task<IResult> ChatAsync(
        string teamId,
        AgentChatRequest request,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return Results.BadRequest(new
            {
                isSuccess = false,
                errorCode = "MessageRequired",
                errorMessage = "Message is required."
            });
        }

        await WriteStreamAsync(
            httpContext,
            teamId,
            request.Message,
            cancellationToken);

        return Results.Empty;
    }

    private async Task WriteStreamAsync(
        HttpContext httpContext,
        string teamId,
        string message,
        CancellationToken cancellationToken)
    {
        httpContext.Response.ContentType = "text/event-stream; charset=utf-8";
        httpContext.Response.Headers.CacheControl = "no-cache";
        httpContext.Response.Headers.Connection = "keep-alive";

        await foreach (var executionEvent in teamRuntime.ExecuteStreamAsync(
                           teamId,
                           message,
                           cancellationToken))
        {
            var streamEvent = TeamChatStreamEventMapper.FromExecutionEvent(executionEvent);
            var payload = JsonSerializer.Serialize(streamEvent, StreamJsonOptions);

            await httpContext.Response.WriteAsync($"data: {payload}\n\n", cancellationToken);
            await httpContext.Response.Body.FlushAsync(cancellationToken);
        }

        await httpContext.Response.WriteAsync("data: [DONE]\n\n", cancellationToken);
        await httpContext.Response.Body.FlushAsync(cancellationToken);
    }
}
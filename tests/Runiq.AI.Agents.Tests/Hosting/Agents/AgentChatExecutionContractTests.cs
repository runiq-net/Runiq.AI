using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Runiq.AI.Agents.Configuration;
using Runiq.AI.Agents.Runtime;
using Runiq.AI.Agents.Tools;
using Runiq.AI.Core;
using Runiq.AI.Core.Agents;

namespace Runiq.AI.Agents.Tests.Hosting.Agents;

public sealed class AgentChatExecutionContractTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("text")]
    [InlineData("json")]
    [InlineData("failed")]
    // Verifies hosted HTTP and SSE payloads retain transport metadata and output when consumed as their own DTO types.
    public async Task HostedPayloads_DeserializeIntoTransportDtos(string outcome)
    {
        var executor = new ControlledExecutor(outcome);
        using var provider = CreateServices(executor).BuildServiceProvider();
        using var scope = provider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<AgentChatApiHandler>();
        var context = CreateContext(scope.ServiceProvider);
        await handler.ChatAsync("agent", new("question", AgentChatResponseMode.Stream), context, CancellationToken.None);
        var payload = Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray());
        var frames = payload.Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
            .Where(frame => frame != "data: [DONE]").Select(frame => frame[6..]).ToArray();
        for (var index = 0; index < frames.Length; index++)
        {
            // SSE declares its wire names explicitly and supports the default serializer options.
            var restored = Assert.IsType<AgentChatStreamEvent>(JsonSerializer.Deserialize<AgentChatStreamEvent>(frames[index]));
            using var document = JsonDocument.Parse(frames[index]);
            var wire = document.RootElement;
            Assert.Equal(executor.Runs[0].RunId, restored.RunId);
            Assert.Equal("agent", restored.AgentId);
            Assert.Equal(wire.GetProperty("status").GetString(), restored.Status?.ToString());
            Assert.Equal(index + 1L, restored.SequenceNumber);
            Assert.Equal(wire.GetProperty("timestamp").GetDateTimeOffset(), restored.Timestamp);
        }
        var terminal = JsonSerializer.Deserialize<AgentChatStreamEvent>(frames[^1])!;
        Assert.Equal(outcome == "failed" ? AgentRunStatus.Failed : AgentRunStatus.Completed, terminal.Status);
        Assert.Equal(outcome == "failed" ? null : outcome == "text" ? "answer" : "", terminal.Message);
        Assert.Equal(outcome == "json" ? 42 : (int?)null, terminal.StructuredOutput?.GetProperty("value").GetInt32());

        var httpResult = await handler.ChatAsync("agent", new("question", AgentChatResponseMode.Result),
            CreateContext(scope.ServiceProvider), CancellationToken.None);
        var response = Assert.IsType<AgentChatResponse>(Assert.IsAssignableFrom<IValueHttpResult>(httpResult).Value);
        // Cover both default round-tripping and the web options used by HTTP clients.
        foreach (var options in new JsonSerializerOptions?[] { null, WebJson })
        {
            var restored = Assert.IsType<AgentChatResponse>(JsonSerializer.Deserialize<AgentChatResponse>(
                JsonSerializer.Serialize(response, options), options));
            Assert.Equal(response.RunId, restored.RunId);
            Assert.Equal(response.AgentId, restored.AgentId);
            Assert.Equal(response.Status, restored.Status);
            Assert.Equal(response.IsSuccess, restored.IsSuccess);
            Assert.Equal(response.Message, restored.Message);
            Assert.Equal(response.ErrorCode, restored.ErrorCode);
            Assert.Equal(response.ErrorMessage, restored.ErrorMessage);
            Assert.Equal(response.Steps.Count, restored.Steps.Count);
            Assert.Equal(response.StructuredOutput?.GetRawText(), restored.StructuredOutput?.GetRawText());
            Assert.Equal(terminal.Message, restored.Message);
            Assert.Equal(terminal.StructuredOutput?.GetRawText(), restored.StructuredOutput?.GetRawText());
        }
    }

    [Fact]
    // Verifies pre-run validation retains the legacy HTTP error shape and never invokes an executor.
    public async Task EmptyMessage_PreservesBadRequestWithoutRunMetadata()
    {
        var executor = new ControlledExecutor("text");
        using var provider = CreateServices(executor).BuildServiceProvider();
        using var scope = provider.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<AgentChatApiHandler>()
            .ChatAsync("agent", new(" ", AgentChatResponseMode.Result), CreateContext(scope.ServiceProvider), CancellationToken.None);
        Assert.Equal(400, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        var response = Assert.IsType<AgentChatResponse>(Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal("MessageRequired", response.ErrorCode);
        var json = JsonSerializer.SerializeToElement(response, WebJson);
        Assert.False(json.TryGetProperty("runId", out _));
        Assert.False(json.TryGetProperty("status", out _));
        Assert.False(json.TryGetProperty("structuredOutput", out _));
        Assert.Equal(0, executor.Invocations);
    }

    [Fact]
    // Verifies standalone legacy events and factory aggregation remain usable without manufactured runtime metadata.
    public void LegacyEvents_PreserveUncorrelatedConsumption()
    {
        var builder = new AgentExecutionResultBuilder();
        foreach (var item in new[] { AgentExecutionEvent.AssistantDelta("legacy"), AgentExecutionEvent.Completed() })
        {
            builder.Apply(item);
            var json = JsonSerializer.SerializeToElement(AgentChatStreamEventMapper.FromExecutionEvent(item), WebJson);
            Assert.False(json.TryGetProperty("runId", out _));
            Assert.False(json.TryGetProperty("sequenceNumber", out _));
            Assert.False(json.TryGetProperty("timestamp", out _));
            Assert.False(json.TryGetProperty("status", out _));
            Assert.False(json.TryGetProperty("structuredOutput", out _));
        }
        var result = builder.Build();
        Assert.True(result.IsSuccess);
        Assert.Equal("legacy", result.Message);
        Assert.Null(result.RunId);
    }

    [Theory]
    [InlineData("text")]
    [InlineData("json")]
    [InlineData("json-null")]
    [InlineData("failed")]
    [InlineData("throw")]
    [InlineData("protocol")]
    // Verifies real SSE serialization and HTTP result projection preserve executor-neutral outcomes without extra execution.
    public async Task HostedExecution_PreservesLegacyFieldsAndAddsRunMetadata(string outcome)
    {
        var executor = new ControlledExecutor(outcome);
        using var provider = CreateServices(executor).BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<AgentChatApiHandler>();
        var context = CreateContext(scope.ServiceProvider);
        await handler.ChatAsync("agent", new("question", AgentChatResponseMode.Stream, "override"), context, CancellationToken.None);
        var payload = Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray());
        Assert.Equal("text/event-stream; charset=utf-8", context.Response.ContentType);
        Assert.EndsWith("data: [DONE]\n\n", payload);
        var frames = payload.Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
            .Where(frame => frame != "data: [DONE]")
            .Select(frame => JsonSerializer.Deserialize<JsonElement>(frame[6..])).ToArray();
        var terminal = frames[^1];
        var succeeded = outcome is "text" or "json" or "json-null";
        Assert.Equal(succeeded ? "completed" : "failed", terminal.GetProperty("type").GetString());
        Assert.Equal(succeeded ? "Completed" : "Failed", terminal.GetProperty("status").GetString());
        Assert.Single(frames, frame => frame.GetProperty("status").GetString() != "Running");
        Assert.All(frames, frame =>
        {
            Assert.Equal(executor.Runs[0].RunId, frame.GetProperty("runId").GetString());
            Assert.Equal("agent", frame.GetProperty("agentId").GetString());
            Assert.Equal(TimeSpan.Zero, frame.GetProperty("timestamp").GetDateTimeOffset().Offset);
        });
        Assert.Equal(Enumerable.Range(1, frames.Length).Select(value => (long)value),
            frames.Select(frame => frame.GetProperty("sequenceNumber").GetInt64()));

        var httpResult = await handler.ChatAsync("agent", new("question", AgentChatResponseMode.Result, "override"),
            CreateContext(scope.ServiceProvider), CancellationToken.None);
        Assert.Equal(200, Assert.IsAssignableFrom<IStatusCodeHttpResult>(httpResult).StatusCode);
        var result = Assert.IsType<AgentChatResponse>(Assert.IsAssignableFrom<IValueHttpResult>(httpResult).Value);
        var resultJson = JsonSerializer.SerializeToElement(result, WebJson);
        Assert.Equal(2, executor.Invocations);
        Assert.All(executor.Queries, query => Assert.Equal("override", query.IndexName));
        Assert.Equal(executor.Runs[1].RunId, result.RunId);
        Assert.NotEqual(executor.Runs[0].RunId, result.RunId);
        Assert.Equal("agent", result.AgentId);
        Assert.Equal(succeeded, result.IsSuccess);
        Assert.Equal(terminal.GetProperty("status").GetString(), resultJson.GetProperty("status").GetString());
        Assert.DoesNotContain("private-provider-secret", payload);
        Assert.DoesNotContain("private-provider-secret", resultJson.GetRawText());
        if (succeeded)
        {
            Assert.Equal(outcome == "text" ? "answer" : "", result.Message);
            Assert.Equal(result.Message, terminal.GetProperty("message").GetString());
            Assert.Equal(JsonValueKind.Null, terminal.GetProperty("content").ValueKind);
            Assert.Equal(outcome != "text", terminal.TryGetProperty("structuredOutput", out var json));
            if (outcome != "text")
                Assert.Equal(json.GetRawText(), resultJson.GetProperty("structuredOutput").GetRawText());
            var calls = result.Steps.Where(step => step.Kind == "tool_call").ToArray();
            Assert.Equal(new[] { "call", "CALL" }, calls.Select(step => step.ToolCallId));
            Assert.Equal(new[] { "completed", "failed" }, calls.Select(step => step.Status));
            Assert.Equal("tool_call_failed", frames[3].GetProperty("type").GetString());
            Assert.Equal("Running", frames[3].GetProperty("status").GetString());
        }
        else
        {
            Assert.Equal(outcome == "protocol" ? "AgentExecutionProtocolError" : "AgentExecutionFailed", result.ErrorCode);
            Assert.Equal(result.ErrorCode, terminal.GetProperty("errorCode").GetString());
            Assert.Equal(result.ErrorMessage, terminal.GetProperty("errorMessage").GetString());
            Assert.Null(result.Message);
            Assert.Null(result.StructuredOutput);
        }
        Assert.Equal(2, executor.Disposals);
    }

    [Theory]
    [InlineData(AgentChatResponseMode.Stream, false)]
    [InlineData(AgentChatResponseMode.Result, false)]
    [InlineData(AgentChatResponseMode.Stream, true)]
    [InlineData(AgentChatResponseMode.Result, true)]
    // Verifies cancellation reaches the caller without a successful result, terminal frame, or DONE marker.
    public async Task HostedCancellation_PropagatesAndDisposes(AgentChatResponseMode mode, bool preCancelled)
    {
        using var cancellation = new CancellationTokenSource();
        var executor = new ControlledExecutor("cancel", cancellation);
        using var provider = CreateServices(executor).BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = CreateContext(scope.ServiceProvider);
        if (preCancelled) cancellation.Cancel();
        var exception = await Assert.ThrowsAsync<AgentRunCanceledException>(() =>
            scope.ServiceProvider.GetRequiredService<AgentChatApiHandler>()
                .ChatAsync("agent", new("question", mode), context, cancellation.Token));
        Assert.Equal(AgentRunStatus.Cancelled, exception.Run.Status);
        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(preCancelled ? 0 : 1, executor.Invocations);
        Assert.Equal(executor.Invocations, executor.Disposals);
        var payload = Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray());
        Assert.DoesNotContain("[DONE]", payload);
        Assert.DoesNotContain("\"type\":\"completed\"", payload);
        Assert.DoesNotContain("\"type\":\"failed\"", payload);
        if (!preCancelled && mode == AgentChatResponseMode.Stream) Assert.Contains("assistant_delta", payload);
    }

    [Fact]
    // Verifies a disconnected SSE writer disposes the paused executor without requesting or delivering a terminal event.
    public async Task SseWriteFailure_AbandonsRunAndCleansUp()
    {
        var executor = new ControlledExecutor("text");
        using var provider = CreateServices(executor).BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = CreateContext(scope.ServiceProvider);
        context.Response.Body = new DisconnectedStream();
        await Assert.ThrowsAsync<IOException>(() => scope.ServiceProvider.GetRequiredService<AgentChatApiHandler>()
            .ChatAsync("agent", new("question", AgentChatResponseMode.Stream), context, CancellationToken.None));
        Assert.Equal(AgentRunStatus.Cancelled, Assert.Single(executor.Runs).Status);
        Assert.Equal(1, executor.Disposals);
        Assert.Equal(1, executor.EventsProduced);
    }

    [Theory]
    [InlineData(null, "AgentExecutorMissing")]
    [InlineData(AgentExecutorKind.Codex, "AgentExecutorNotSupported")]
    [InlineData(AgentExecutorKind.Claude, "AgentExecutorNotSupported")]
    // Verifies selection failures remain normal HTTP and SSE failures through the existing handler.
    public async Task HostedSelectionFailures_AreCompatible(AgentExecutorKind? kind, string errorCode)
    {
        var services = new ServiceCollection();
        var agent = new Agent("agent", "Agent", "instructions");
        if (kind == AgentExecutorKind.Codex) agent.UseCodex();
        if (kind == AgentExecutorKind.Claude) agent.UseClaude();
        services.AddSingleton(agent);
        services.AddRuniqAgentServer();
        services.RemoveAll<IAgentExecutor>();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<AgentChatApiHandler>();
        var response = await handler.ChatAsync("agent", new("question", AgentChatResponseMode.Result), CreateContext(scope.ServiceProvider), CancellationToken.None);
        var value = Assert.IsType<AgentChatResponse>(Assert.IsAssignableFrom<IValueHttpResult>(response).Value);
        Assert.False(value.IsSuccess);
        Assert.Equal(AgentRunStatus.Failed, value.Status);
        Assert.Equal(errorCode, value.ErrorCode);
        var context = CreateContext(scope.ServiceProvider);
        await handler.ChatAsync("agent", new("question", AgentChatResponseMode.Stream), context, CancellationToken.None);
        var payload = Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray());
        Assert.Contains($"\"errorCode\":\"{errorCode}\"", payload);
        Assert.Contains("\"type\":\"failed\"", payload);
        Assert.EndsWith("data: [DONE]\n\n", payload);
    }

    private static ServiceCollection CreateServices(ControlledExecutor executor)
    {
        var services = new ServiceCollection();
        services.AddSingleton(new Agent("agent", "Agent", "instructions").UseCodex());
        services.AddRuniqAgentServer();
        services.RemoveAll<IAgentExecutor>();
        services.AddScoped<IAgentExecutor>(_ => executor);
        return services;
    }

    private static DefaultHttpContext CreateContext(IServiceProvider services) =>
        new() { RequestServices = services, Response = { Body = new MemoryStream() } };

    private sealed class DisconnectedStream : MemoryStream
    {
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromException(new IOException("Client disconnected."));
    }

    private sealed class ControlledExecutor(string outcome, CancellationTokenSource? cancellation = null) : IAgentExecutor
    {
        internal int Invocations, Disposals, EventsProduced;
        internal List<AgentRunContext> Runs { get; } = [];
        internal List<AgentQuery> Queries { get; } = [];
        public AgentExecutorKind Kind => AgentExecutorKind.Codex;
        public async IAsyncEnumerable<AgentExecutionEvent> ExecuteAsync(AgentExecutionRequest request, AgentRunContext run,
            AgentToolInvoker toolInvoker, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Invocations++;
            Runs.Add(run);
            Queries.Add(request.Query);
            try
            {
                if (outcome == "throw") throw new InvalidOperationException("private-provider-secret");
                if (outcome == "failed") { yield return AgentExecutionEvent.Failed("safe failure"); yield break; }
                if (outcome == "cancel")
                {
                    yield return AgentExecutionEvent.AssistantDelta("partial");
                    cancellation!.Cancel();
                    cancellationToken.ThrowIfCancellationRequested();
                }
                if (outcome == "protocol") yield break;
                foreach (var item in new[]
                {
                    AgentExecutionEvent.ToolCallStarted("call", "lookup", "{}"),
                    AgentExecutionEvent.ToolCallCompleted("call", "lookup", "first"),
                    AgentExecutionEvent.ToolCallStarted("CALL", "lookup", "{}"),
                    AgentExecutionEvent.ToolCallFailed("CALL", "lookup", "safe tool failure", "ToolFailure")
                })
                {
                    EventsProduced++;
                    yield return item;
                    await Task.Yield();
                }
                if (outcome == "text") yield return AgentExecutionEvent.AssistantDelta("answer");
                AgentExecutionEvent completion;
                using (var document = JsonDocument.Parse(outcome == "json-null" ? "null" : "{\"value\":42}"))
                    completion = outcome == "text" ? AgentExecutionEvent.Completed() : AgentExecutionEvent.Completed(null, [], document.RootElement);
                yield return completion;
            }
            finally { Disposals++; }
        }
    }
}

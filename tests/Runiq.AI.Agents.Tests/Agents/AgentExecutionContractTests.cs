using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Runiq.AI.Agents.Configuration;
using Runiq.AI.Agents.Runtime;
using Runiq.AI.Agents.Tests.TestDoubles;
using Runiq.AI.Agents.Tools;
using Runiq.AI.Core;
using Runiq.AI.Core.AI.Chat;
using Runiq.AI.Rag.Models.Retrieval;
using Runiq.AI.Rag.Models.Search;

namespace Runiq.AI.Agents.Tests.Agents;

public sealed class AgentExecutionContractTests
{
    [Fact]
    // Verifies overlapping calls on one runtime isolate identical tool IDs, sequence counters, and per-run aggregation.
    public async Task ConcurrentRuns_IsolateToolsAndMatchTheirOwnTerminal()
    {
        var executor = new InterleavedExecutor();
        var services = CreateServices();
        services.AddScoped<IAgentExecutor>(_ => executor);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var runtime = scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var streaming = CollectAsync(runtime.ExecuteStreamAsync("agent", "stream", cancellationToken: timeout.Token));
        var batch = runtime.ExecuteAsync("agent", "batch", timeout.Token);
        await Task.WhenAll(streaming, batch);
        var events = await streaming;
        var result = await batch;
        var builder = new AgentExecutionResultBuilder();
        events.ForEach(builder.Apply);
        var fromStream = builder.Build();
        Assert.NotEqual(result.RunId, fromStream.RunId);
        Assert.Equal("batch", result.Message);
        Assert.Equal("stream", fromStream.Message);
        Assert.Equal(events[^1].RunId, fromStream.RunId);
        Assert.Equal(events[^1].Status, fromStream.Status);
        Assert.Equal(events[^1].Message, fromStream.Message);
        Assert.All(events, item => Assert.Equal(fromStream.RunId, item.RunId));
        Assert.Equal(new long?[] { 1, 2, 3, 4 }, events.Select(item => item.SequenceNumber));
        foreach (var aggregate in new[] { result, fromStream })
        {
            var tool = Assert.Single(aggregate.Steps, item => item.Kind == AgentExecutionStepKind.ToolCall);
            Assert.Equal("same-call", tool.ToolCallId);
            Assert.Equal(aggregate.Message, tool.OutputJson);
            Assert.Equal("agent", aggregate.AgentId);
            Assert.Equal(AgentRunStatus.Completed, aggregate.Status);
        }
        Assert.Equal(2, executor.Invocations);
    }

    [Theory]
    [InlineData("{\"value\":42}")]
    [InlineData("[1,2]")]
    [InlineData("null")]
    // Verifies explicit JSON is independently owned by both factory products after source disposal.
    public void StructuredOutput_FactoriesCloneJson(string json)
    {
        AgentExecutionEvent completion;
        AgentExecutionResult result;
        using (var document = JsonDocument.Parse(json))
        {
            completion = AgentExecutionEvent.Completed(null, [], document.RootElement);
            result = AgentExecutionResult.Success("text", [], null, [], document.RootElement);
        }
        Assert.Equal(json, completion.StructuredOutput!.Value.GetRawText());
        Assert.Equal(json, result.StructuredOutput!.Value.GetRawText());
        Assert.Contains(json, JsonSerializer.Serialize(result), StringComparison.Ordinal);
        Assert.Null(completion.SequenceNumber);
        Assert.Null(completion.Timestamp);
        Assert.Null(AgentExecutionResult.Success(json).StructuredOutput);
    }

    [Fact]
    // Verifies an undefined element is rejected rather than retained as an unserializable JSON payload.
    public void StructuredOutput_RejectsUndefinedJson()
    {
        Assert.Throws<ArgumentException>(() => AgentExecutionEvent.Completed(null, [], default(JsonElement)));
        Assert.Throws<ArgumentException>(() => AgentExecutionResult.Success("text", [], null, [], default(JsonElement)));
    }

    [Fact]
    // Verifies explicit output, RAG, citations, and repeated same-name tools survive terminal-event aggregation.
    public async Task StructuredCompletion_PreservesMetadataAndMatchesAggregate()
    {
        var rag = new AgentRagExecutionMetadata(RagExecutionMode.Open, false,
            RagNoContextBehavior.AnswerNormally, null, true, false, [], [], [],
            RagRetrievalMode.Semantic, RagRetrievalStatistics.Empty);
        var citation = new AgentCitation(1, "document", "chunk", "retrieval", 0, 1);
        var services = CreateServices();
        services.AddScoped<IAgentExecutor>(_ => new TestExecutor(AgentExecutorKind.Codex, () =>
        {
            using var json = JsonDocument.Parse("{\"value\":42}");
            return [
                AgentExecutionEvent.ToolCallStarted("call", "lookup", "{}"),
                AgentExecutionEvent.ToolCallStarted("CALL", "lookup", "{}"),
                AgentExecutionEvent.ToolCallCompleted("CALL", "lookup", "second"),
                AgentExecutionEvent.ToolCallCompleted("call", "lookup", "first"),
                AgentExecutionEvent.AssistantDelta("answer "),
                AgentExecutionEvent.AssistantDelta("[1]"),
                AgentExecutionEvent.Completed(rag, [citation], json.RootElement)
            ];
        }));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var runtime = scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>();
        var before = DateTimeOffset.UtcNow;
        var events = await CollectAsync(runtime.ExecuteStreamAsync("agent", "question"));
        var after = DateTimeOffset.UtcNow;
        var terminal = events[^1];
        var builder = new AgentExecutionResultBuilder();
        events.ForEach(builder.Apply);
        var result = builder.Build();
        var batch = await runtime.ExecuteAsync("agent", "question");

        Assert.Equal(Enumerable.Range(1, events.Count).Select(number => (long?)number), events.Select(item => item.SequenceNumber));
        Assert.All(events, item =>
        {
            Assert.Equal(result.RunId, item.RunId);
            Assert.InRange(item.Timestamp!.Value, before, after);
            Assert.Equal(TimeSpan.Zero, item.Timestamp.Value.Offset);
        });
        Assert.Equal(terminal.Message, result.Message);
        Assert.Equal("answer [1]", result.Message);
        Assert.Equal(terminal.Status, result.Status);
        Assert.Equal(terminal.ErrorCode, result.ErrorCode);
        Assert.Equal(terminal.StructuredOutput!.Value.GetRawText(), result.StructuredOutput!.Value.GetRawText());
        Assert.Equal(result.Message, batch.Message);
        Assert.Equal(result.StructuredOutput.Value.GetRawText(), batch.StructuredOutput!.Value.GetRawText());
        Assert.Same(rag, result.Rag);
        Assert.Same(citation, Assert.Single(result.Citations));
        var tools = result.Steps.Where(step => step.Kind == AgentExecutionStepKind.ToolCall).ToArray();
        Assert.Equal(2, tools.Length);
        Assert.Equal("first", tools.Single(step => step.ToolCallId == "call").OutputJson);
        Assert.Equal("second", tools.Single(step => step.ToolCallId == "CALL").OutputJson);
        Assert.All(tools, step => Assert.Equal(AgentExecutionStepStatus.Completed, step.Status));
        Assert.Throws<InvalidOperationException>(() => builder.Apply(terminal));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    // Verifies JSON-only execution succeeds only with explicit structured output, never by guessing from text.
    public async Task JsonOutput_RequiresExplicitExecutorPayload(bool explicitJson)
    {
        var services = CreateServices();
        services.AddScoped<IAgentExecutor>(_ => new TestExecutor(AgentExecutorKind.Codex, () =>
        {
            using var json = JsonDocument.Parse("{\"value\":42}");
            return explicitJson
                ? [AgentExecutionEvent.Completed(null, [], json.RootElement)]
                : [AgentExecutionEvent.AssistantDelta(json.RootElement.GetRawText()), AgentExecutionEvent.Completed()];
        }));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>().ExecuteAsync("agent", "question");
        Assert.True(result.IsSuccess);
        Assert.Equal(explicitJson, result.StructuredOutput.HasValue);
        Assert.Equal(explicitJson ? "" : "{\"value\":42}", result.Message);
    }

    [Fact]
    // Verifies partial cancelled streams and out-of-order events cannot become apparently successful results.
    public async Task Builder_RejectsIncompleteCancelledAndOutOfOrderRuns()
    {
        var services = CreateServices();
        services.AddScoped<IAgentExecutor>(_ => new TestExecutor(AgentExecutorKind.Codex,
            () => [AgentExecutionEvent.AssistantDelta("partial"), AgentExecutionEvent.Completed()]));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var runtime = scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>();
        await using var stream = runtime.ExecuteStreamAsync("agent", "question", cancellationToken: cancellation.Token).GetAsyncEnumerator();
        Assert.True(await stream.MoveNextAsync());
        var runId = stream.Current.RunId;
        var builder = new AgentExecutionResultBuilder();
        builder.Apply(stream.Current);
        Assert.Throws<InvalidOperationException>(() => builder.Build());
        cancellation.Cancel();
        var failure = await Assert.ThrowsAsync<AgentRunCanceledException>(async () => await stream.MoveNextAsync());
        Assert.Equal(AgentRunStatus.Cancelled, failure.Run.Status);
        Assert.Equal(runId, failure.Run.RunId);
        Assert.Throws<InvalidOperationException>(() => builder.Build());
        var complete = await CollectAsync(runtime.ExecuteStreamAsync("agent", "question"));
        Assert.Throws<InvalidOperationException>(() => new AgentExecutionResultBuilder().Apply(complete[^1]));
    }

    [Theory]
    [InlineData(AgentExecutorKind.Model)]
    [InlineData(AgentExecutorKind.Codex)]
    [InlineData(AgentExecutorKind.Claude)]
    // Verifies dispatch by the registered kind, query preservation, and one common path for both runtime APIs.
    public async Task Registry_DispatchesEachRegisteredKind(AgentExecutorKind kind)
    {
        var services = CreateServices(kind);
        services.RemoveAll<IAgentExecutor>();
        var requests = new List<AgentExecutionRequest>();
        services.AddScoped<IAgentExecutor>(_ => new TestExecutor(kind,
            () => [AgentExecutionEvent.AssistantDelta(kind.ToString()), AgentExecutionEvent.Completed()], requests.Add));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        var runtime = scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>();
        var query = new AgentQuery("question") { IndexName = "override" };
        var result = await runtime.ExecuteAsync("agent", query);
        var events = await CollectAsync(runtime.ExecuteStreamAsync("agent", query));
        Assert.Equal(kind.ToString(), result.Message);
        Assert.Equal(result.Message, events[^1].Message);
        Assert.Equal(2, requests.Count);
        Assert.All(requests, request => Assert.Same(query, request.Query));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    // Verifies neither repeated identical registrations nor different implementations for the same kind silently win.
    public void Registry_RejectsDuplicateKinds(bool sameInstance)
    {
        var services = CreateServices();
        var executor = new TestExecutor(AgentExecutorKind.Codex, () => []);
        services.AddScoped<IAgentExecutor>(_ => executor);
        services.AddScoped<IAgentExecutor>(_ => sameInstance ? executor : new OtherCodexExecutor());
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var exception = Assert.Throws<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>());
        Assert.Contains("Multiple agent executors", exception.Message);
        Assert.Contains("Codex", exception.Message);
    }

    [Fact]
    // Verifies repeated host setup does not duplicate the built-in model and custom model registration cannot silently override it.
    public void Registry_BuiltinRegistrationIsIdempotentAndDetectsModelConflict()
    {
        var services = CreateServices();
        services.AddRuniqAgentServer();
        using (var provider = services.BuildServiceProvider())
        using (var scope = provider.CreateScope())
            Assert.Single(scope.ServiceProvider.GetServices<IAgentExecutor>());
        services.AddScoped<IAgentExecutor>(_ => new TestExecutor(AgentExecutorKind.Model, () => []));
        using var conflicting = services.BuildServiceProvider();
        using var conflictScope = conflicting.CreateScope();
        var exception = Assert.Throws<InvalidOperationException>(() => conflictScope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>());
        Assert.Contains("Model", exception.Message);
    }

    [Theory]
    [InlineData(null, "AgentExecutorMissing")]
    [InlineData(AgentExecutorKind.Codex, "AgentExecutorNotSupported")]
    [InlineData(AgentExecutorKind.Claude, "AgentExecutorNotSupported")]
    [InlineData(AgentExecutorKind.Model, "AgentExecutorNotSupported")]
    // Verifies absent selection differs from absent implementation and both stop before RAG or provider work.
    public async Task Registry_MissingSelectionAndImplementationProduceDistinctFailures(AgentExecutorKind? kind, string code)
    {
        var services = CreateServices(kind);
        if (kind == AgentExecutorKind.Model) services.RemoveAll<IAgentExecutor>();
        var resolver = new TestChatClientResolver();
        services.AddScoped<IChatClientResolver>(_ => resolver);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var runtime = scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>();
        var result = await runtime.ExecuteAsync("agent", "question");
        var terminal = Assert.Single(await CollectAsync(runtime.ExecuteStreamAsync("agent", "question")));
        Assert.False(result.IsSuccess);
        Assert.Equal(code, result.ErrorCode);
        Assert.Equal(result.ErrorCode, terminal.ErrorCode);
        Assert.Equal(result.Status, terminal.Status);
        Assert.Equal(1, terminal.SequenceNumber);
        Assert.NotNull(terminal.Timestamp);
        Assert.Empty(resolver.Requests);
    }

    [Fact]
    // Verifies custom executors resolve scoped dependencies per scope without capturing them in a singleton registry.
    public async Task Registry_PreservesScopedDependencies()
    {
        var services = CreateServices();
        services.AddScoped<ScopeProbe>();
        services.AddScoped<IAgentExecutor>(provider =>
        {
            var probe = provider.GetRequiredService<ScopeProbe>();
            return new TestExecutor(AgentExecutorKind.Codex,
                () => [AgentExecutionEvent.AssistantDelta(probe.Id), AgentExecutionEvent.Completed()]);
        });
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        var firstRuntime = first.ServiceProvider.GetRequiredService<AgentExecutionRuntime>();
        var secondRuntime = second.ServiceProvider.GetRequiredService<AgentExecutionRuntime>();
        var one = await firstRuntime.ExecuteAsync("agent", "question");
        var repeat = await firstRuntime.ExecuteAsync("agent", "question");
        var two = await secondRuntime.ExecuteAsync("agent", "question");
        Assert.Equal(one.Message, repeat.Message);
        Assert.NotEqual(one.Message, two.Message);
        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<AgentExecutionRuntime>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    // Verifies executor startup faults and missing terminal events become correlated failures through either API.
    public async Task CustomExecutor_InvalidExecutionEndsWithOneFailure(bool startupFault)
    {
        var services = CreateServices();
        services.AddScoped<IAgentExecutor>(_ => startupFault
            ? new OtherCodexExecutor()
            : new TestExecutor(AgentExecutorKind.Codex, () => [AgentExecutionEvent.AssistantDelta("partial")]));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var runtime = scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>();
        var events = await CollectAsync(runtime.ExecuteStreamAsync("agent", "question"));
        var result = await runtime.ExecuteAsync("agent", "question");
        Assert.Single(events, item => item.Status != AgentRunStatus.Running);
        Assert.Equal(AgentRunStatus.Failed, events[^1].Status);
        Assert.Equal(startupFault ? "AgentExecutionFailed" : "AgentExecutionProtocolError", events[^1].ErrorCode);
        Assert.Equal(result.ErrorCode, events[^1].ErrorCode);
        Assert.Equal(result.ErrorMessage, events[^1].ErrorMessage);
        Assert.False(result.IsSuccess);
        Assert.Null(result.Message);
    }

    [Fact]
    // Verifies the runtime publishes one terminal event even if a custom executor attempts to continue afterward.
    public async Task CustomExecutor_CannotPublishEventsAfterCompletion()
    {
        var services = CreateServices();
        services.AddScoped<IAgentExecutor>(_ => new TestExecutor(AgentExecutorKind.Codex, () =>
        [
            AgentExecutionEvent.AssistantDelta("answer"), AgentExecutionEvent.Completed(),
            AgentExecutionEvent.Failed("late failure", "LateFailure")
        ]));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var events = await CollectAsync(scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>()
            .ExecuteStreamAsync("agent", "question"));
        Assert.Equal(2, events.Count);
        Assert.Equal(AgentRunStatus.Completed, events[^1].Status);
        Assert.Equal("answer", events[^1].Message);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    // Verifies either direction of identity mixing is rejected without assigning legacy success to a cancelled run.
    public async Task Builder_RejectsIdentityMixingWithoutChangingAcceptedState(bool correlatedFirst, bool legacyCompleted)
    {
        var services = CreateServices();
        services.AddScoped<IAgentExecutor>(_ => new TestExecutor(AgentExecutorKind.Codex,
            () => [AgentExecutionEvent.AssistantDelta("partial"), AgentExecutionEvent.Completed()]));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        using var cancellation = new CancellationTokenSource();
        await using var stream = scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>()
            .ExecuteStreamAsync("agent", "question", cancellationToken: cancellation.Token).GetAsyncEnumerator();
        Assert.True(await stream.MoveNextAsync());
        var correlated = stream.Current;
        var legacy = legacyCompleted ? AgentExecutionEvent.Completed() : AgentExecutionEvent.AssistantDelta("legacy");
        var builder = new AgentExecutionResultBuilder();
        builder.Apply(correlatedFirst ? correlated : legacy);
        Assert.Throws<InvalidOperationException>(() => builder.Apply(correlatedFirst ? legacy : correlated));

        cancellation.Cancel();
        var cancellationFailure = await Assert.ThrowsAsync<AgentRunCanceledException>(async () => await stream.MoveNextAsync());
        Assert.Equal(correlated.RunId, cancellationFailure.Run.RunId);
        Assert.Equal(AgentRunStatus.Cancelled, cancellationFailure.Run.Status);
        if (correlatedFirst)
        {
            Assert.Throws<InvalidOperationException>(() => builder.Build());
        }
        else
        {
            var legacyResult = builder.Build();
            Assert.Null(legacyResult.RunId);
            Assert.Null(legacyResult.AgentId);
            Assert.DoesNotContain("partial", legacyResult.Message!);
            // Pure legacy aggregation remains available after rejecting the foreign event.
            builder.Apply(AgentExecutionEvent.Completed());
            Assert.True(builder.Build().IsSuccess);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("ExecutorSpecificFailure")]
    [InlineData("")]
    // Verifies omitted error codes are normalized before publication while explicitly provided codes remain unchanged.
    public async Task CustomExecutor_FailureTerminalMatchesAggregate(string? errorCode)
    {
        var services = CreateServices();
        services.AddScoped<IAgentExecutor>(_ => new TestExecutor(AgentExecutorKind.Codex,
            () => [AgentExecutionEvent.Failed("executor failure", errorCode)]));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var runtime = scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>();
        var terminal = Assert.Single(await CollectAsync(runtime.ExecuteStreamAsync("agent", "question")));
        var result = await runtime.ExecuteAsync("agent", "question");
        var builder = new AgentExecutionResultBuilder();
        builder.Apply(terminal);
        var fromStream = builder.Build();

        Assert.Equal(errorCode ?? "AgentExecutionFailed", terminal.ErrorCode);
        Assert.Equal("executor failure", terminal.ErrorMessage);
        Assert.Equal(AgentRunStatus.Failed, terminal.Status);
        foreach (var aggregate in new[] { result, fromStream })
        {
            Assert.False(aggregate.IsSuccess);
            Assert.Equal(terminal.Status, aggregate.Status);
            Assert.Equal(terminal.ErrorCode, aggregate.ErrorCode);
            Assert.Equal(terminal.ErrorMessage, aggregate.ErrorMessage);
        }
    }

    private static ServiceCollection CreateServices(AgentExecutorKind? kind = AgentExecutorKind.Codex)
    {
        var agent = new Agent("agent", "Agent", "instructions");
        switch (kind)
        {
            case AgentExecutorKind.Model: agent.UseModel("openai/model", "key"); break;
            case AgentExecutorKind.Codex: agent.UseCodex(); break;
            case AgentExecutorKind.Claude: agent.UseClaude(); break;
        }
        // Invalid RAG settings would fail if an unsupported selection reached the model pipeline.
        agent.UseRag(options => options.IndexName = "documents");
        var services = new ServiceCollection();
        services.AddSingleton(agent);
        services.AddScoped<IChatClientResolver, TestChatClientResolver>();
        services.AddRuniqAgentServer();
        return services;
    }

    private static async Task<List<AgentExecutionEvent>> CollectAsync(IAsyncEnumerable<AgentExecutionEvent> source)
    {
        var events = new List<AgentExecutionEvent>();
        await foreach (var item in source) events.Add(item);
        return events;
    }

    private sealed class ScopeProbe
    {
        public string Id { get; } = Guid.NewGuid().ToString("N");
    }

    private sealed class InterleavedExecutor : IAgentExecutor
    {
        private readonly TaskCompletionSource bothStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Invocations;
        public AgentExecutorKind Kind => AgentExecutorKind.Codex;
        public async IAsyncEnumerable<AgentExecutionEvent> ExecuteAsync(AgentExecutionRequest request,
            AgentRunContext run, AgentToolInvoker toolInvoker, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref Invocations) == 2) bothStarted.TrySetResult();
            yield return AgentExecutionEvent.ToolCallStarted("same-call", "lookup", "{}");
            await bothStarted.Task.WaitAsync(cancellationToken);
            yield return AgentExecutionEvent.ToolCallCompleted("same-call", "lookup", request.Query.Message);
            yield return AgentExecutionEvent.AssistantDelta(request.Query.Message);
            yield return AgentExecutionEvent.Completed();
        }
    }

    private sealed class TestExecutor(AgentExecutorKind kind, Func<AgentExecutionEvent[]> events,
        Action<AgentExecutionRequest>? onRequest = null) : IAgentExecutor
    {
        public AgentExecutorKind Kind => kind;

        public async IAsyncEnumerable<AgentExecutionEvent> ExecuteAsync(AgentExecutionRequest request,
            AgentRunContext run, AgentToolInvoker toolInvoker, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            onRequest?.Invoke(request);
            foreach (var item in events())
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return item;
                await Task.Yield();
            }
        }
    }

    private sealed class OtherCodexExecutor : IAgentExecutor
    {
        public AgentExecutorKind Kind => AgentExecutorKind.Codex;
        public IAsyncEnumerable<AgentExecutionEvent> ExecuteAsync(AgentExecutionRequest request,
            AgentRunContext run, AgentToolInvoker toolInvoker, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Duplicate registration must prevent execution.");
    }
}

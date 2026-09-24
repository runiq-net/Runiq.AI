using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Runiq.AI.Agents.Configuration;
using Runiq.AI.Agents.Runtime;
using Runiq.AI.Agents.Runtime.Codex;
using Runiq.AI.Core;
using Runiq.AI.Core.Agents;

namespace Runiq.AI.Agents.Tests.Agents;

public sealed class CodexExecutorTests
{
    private const string Session = "0199a213-81c0-7800-8aa1-bbab2a035a53";
    private const string Start = "{\"type\":\"thread.started\",\"thread_id\":\"" + Session + "\"}\n{\"type\":\"turn.started\"}\n";
    private const string Message = "{\"type\":\"item.completed\",\"item\":{\"id\":\"1\",\"type\":\"agent_message\",\"text\":\"hello\"}}\n";
    private const string End = "{\"type\":\"turn.completed\"}\n";

    [Fact]
    // Verifies opt-in registration, independent run identities, streaming order and real session propagation.
    public async Task Execution_StreamsAndAggregatesSamePayloadWithConfirmedSession()
    {
        var factory = new FakeFactory(() => new FakeProcess(Start + Message + End));
        await using var services = Services(factory);
        await using var scope = services.CreateAsyncScope();
        var runtime = scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>();
        var events = new List<AgentExecutionEvent>();
        await foreach (var item in runtime.ExecuteStreamAsync("codex", "hello")) events.Add(item);
        Assert.Equal(new[] { AgentExecutionEventKind.AssistantDelta, AgentExecutionEventKind.Completed }, events.Select(x => x.Kind));
        Assert.Equal(new long?[] { 1, 2 }, events.Select(x => x.SequenceNumber));
        Assert.All(events, item => Assert.Equal(Session, item.ProviderSessionId));
        Assert.Null(events[0].EndedAt);
        Assert.NotNull(events[1].EndedAt);
        var result = await runtime.ExecuteAsync("codex", "hello");
        Assert.True(result.IsSuccess);
        Assert.Equal(events[^1].Message, result.Message);
        Assert.Equal(Session, result.ProviderSessionId);
        Assert.NotEqual(events[0].RunId, result.RunId);
        Assert.All(factory.Processes, process => Assert.Equal(1, process.DisposeCount));
        Assert.All(factory.Processes, process => Assert.Contains("Agent instructions:\ninstructions", process.Input));
        Assert.DoesNotContain("hello", factory.Command!.ArgumentList);
        Assert.False(factory.Command.UseShellExecute);
        Assert.False(factory.Command.Environment.ContainsKey("CODEX_API_KEY"));
        Assert.False(factory.Command.Environment.ContainsKey("OPENAI_API_KEY"));
        var mapped = AgentChatStreamEventMapper.FromExecutionEvent(events[0]);
        Assert.Equal(Session, mapped.ProviderSessionId);
        Assert.Contains("providerSessionId", JsonSerializer.Serialize(mapped));
    }

    [Fact]
    // Verifies explicit resume arguments and fresh runtime identity without fabricating a new provider session.
    public async Task Continuation_UsesExactSessionAndFreshRun()
    {
        var factory = new FakeFactory(() => new FakeProcess(Start + Message + End));
        await using var services = Services(factory);
        await using var scope = services.CreateAsyncScope();
        var runtime = scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>();
        var initial = await runtime.ExecuteAsync("codex", "first");
        var resumed = await runtime.ExecuteAsync("codex", new AgentQuery("follow-up") { ProviderSessionId = initial.ProviderSessionId });
        Assert.True(resumed.IsSuccess);
        Assert.Equal(Session, resumed.ProviderSessionId);
        Assert.NotEqual(initial.RunId, resumed.RunId);
        Assert.Equal(new[] { "resume", Session, "-" }, factory.Command!.ArgumentList.TakeLast(3));
        Assert.DoesNotContain("--last", factory.Command.ArgumentList);
    }

    [Theory]
    [InlineData("not-json", 0, "", "CodexOutputInvalid")]
    [InlineData("{}", 0, "", "CodexOutputInvalid")]
    [InlineData("", 0, "", "CodexOutputInvalid")]
    [InlineData(Start + Message, 0, "", "CodexOutputInvalid")]
    [InlineData(Start + Message + End, 7, "", "CodexProcessFailed")]
    [InlineData("", 1, "Not logged in: private-token", "CodexAuthenticationFailed")]
    [InlineData("", 1, "Error loading config.toml private-path", "CodexConfigurationInvalid")]
    [InlineData("", 1, "No session found", "CodexSessionNotFound")]
    [InlineData("{\"type\":\"turn.failed\",\"error\":{\"message\":\"unauthorized private-token\"}}", 1, "", "CodexAuthenticationFailed")]
    [InlineData(Start + Message + End + Message, 0, "", "CodexOutputInvalid")]
    [InlineData(Start + Message + Message + End, 0, "", "CodexOutputInvalid")]
    // Verifies malformed protocol and process failures become safe, correlated terminal errors.
    public async Task Failures_AreNormalized(string output, int exitCode, string stderr, string expected)
    {
        var factory = new FakeFactory(() => new FakeProcess(output, exitCode, stderr));
        await using var services = Services(factory);
        await using var scope = services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>().ExecuteAsync("codex", "input");
        Assert.Equal(expected, result.ErrorCode);
        Assert.Equal(AgentRunStatus.Failed, result.Status);
        Assert.NotNull(result.RunId);
        Assert.DoesNotContain("private", result.ErrorMessage!);
        Assert.Equal(1, Assert.Single(factory.Processes).DisposeCount);
    }

    [Fact]
    // Verifies transient errors and additive telemetry do not override a subsequently successful turn.
    public async Task Protocol_AllowsTransientErrorAndUnknownTelemetry()
    {
        var output = Start + "{\"type\":\"error\",\"message\":\"retrying\"}\n{\"type\":\"new.telemetry\"}\n" + Message + End;
        await using var services = Services(new FakeFactory(() => new FakeProcess(output)));
        await using var scope = services.CreateAsyncScope();
        Assert.True((await scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>().ExecuteAsync("codex", "input")).IsSuccess);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    // Verifies timeout is a failure while caller cancellation preserves the runtime exception contract.
    public async Task WaitingProcess_TimeoutAndCancellationStopExecution(bool callerCancels)
    {
        var factory = new FakeFactory(() => new FakeProcess(Start, wait: true));
        await using var services = Services(factory, options => options.Timeout = TimeSpan.FromMilliseconds(callerCancels ? 5000 : 50));
        await using var scope = services.CreateAsyncScope();
        using var cancellation = new CancellationTokenSource();
        var task = scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>().ExecuteAsync("codex", "input", cancellation.Token);
        if (callerCancels)
        {
            cancellation.Cancel();
            var failure = await Assert.ThrowsAsync<AgentRunCanceledException>(() => task);
            Assert.Equal(AgentRunStatus.Cancelled, failure.Run.Status);
            Assert.Equal(Session, failure.Run.ProviderSessionId);
        }
        else Assert.Equal("CodexTimeout", (await task).ErrorCode);
        var process = Assert.Single(factory.Processes);
        Assert.True(process.Token.IsCancellationRequested);
        Assert.Equal(1, process.DisposeCount);
    }

    [Fact]
    // Verifies live output arrives before exit, concurrent resume is rejected, and disposal releases the session.
    public async Task PartialStream_DisposalStopsProcessAndReleasesResumeGate()
    {
        var factory = new FakeFactory(() => new FakeProcess(Start + Message, wait: true));
        await using var services = Services(factory);
        await using var scope = services.CreateAsyncScope();
        var runtime = scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>();
        var query = new AgentQuery("input") { ProviderSessionId = Session };
        var stream = runtime.ExecuteStreamAsync("codex", query).GetAsyncEnumerator();
        Assert.True(await stream.MoveNextAsync());
        Assert.Equal("hello", stream.Current.Content);
        Assert.Equal(0, factory.Processes[0].DisposeCount);
        var busy = await runtime.ExecuteAsync("codex", query);
        Assert.Equal("CodexSessionBusy", busy.ErrorCode);
        await stream.DisposeAsync();
        Assert.Equal(1, factory.Processes[0].DisposeCount);
        Assert.True(factory.Processes[0].Token.IsCancellationRequested);
        await using var next = runtime.ExecuteStreamAsync("codex", query).GetAsyncEnumerator();
        Assert.True(await next.MoveNextAsync());
        Assert.Equal(2, factory.Processes.Count);
    }

    [Theory]
    [InlineData(2, "CodexExecutableNotFound")]
    [InlineData(5, "CodexProcessStartFailed")]
    // Verifies executable disappearance and OS start rejection remain distinguishable.
    public async Task StartFailure_MapsOperatingSystemError(int nativeCode, string expected)
    {
        var factory = new FakeFactory(() => throw new Win32Exception(nativeCode));
        await using var services = Services(factory);
        await using var scope = services.CreateAsyncScope();
        Assert.Equal(expected, (await scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>().ExecuteAsync("codex", "input")).ErrorCode);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("--last")]
    [InlineData("")]
    // Verifies unsafe or ambiguous session identifiers are rejected before starting a process.
    public async Task InvalidSession_DoesNotStartProcess(string sessionId)
    {
        var factory = new FakeFactory(() => new FakeProcess(""));
        await using var services = Services(factory);
        await using var scope = services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>()
            .ExecuteAsync("codex", new AgentQuery("input") { ProviderSessionId = sessionId });
        Assert.Equal("CodexSessionInvalid", result.ErrorCode);
        Assert.Empty(factory.Processes);
    }

    [Fact]
    // Verifies a CLI fallback to another session is detected rather than presented as successful continuation.
    public async Task UnexpectedSession_FailsWithoutPublishingFalseIdentity()
    {
        await using var services = Services(new FakeFactory(() => new FakeProcess(Start + Message + End)));
        await using var scope = services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>()
            .ExecuteAsync("codex", new AgentQuery("input") { ProviderSessionId = Guid.NewGuid().ToString() });
        Assert.Equal("CodexSessionInvalid", result.ErrorCode);
        Assert.Null(result.ProviderSessionId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    // Verifies both a giant record and cumulative stdout are bounded before aggregation.
    public async Task OutputLimits_StopProcess(bool giantRecord)
    {
        var output = giantRecord ? new string('x', 129) : string.Concat(Enumerable.Repeat("{\"type\":\"telemetry\"}\n", 50));
        var factory = new FakeFactory(() => new FakeProcess(output));
        await using var services = Services(factory, options => { options.MaxEventCharacters = 128; options.MaxOutputCharacters = 256; });
        await using var scope = services.CreateAsyncScope();
        Assert.Equal("CodexOutputLimitExceeded", (await scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>().ExecuteAsync("codex", "input")).ErrorCode);
        Assert.Equal(1, factory.Processes[0].DisposeCount);
    }

    [Fact]
    // Verifies repeated registration is idempotent and default hosts retain their unsupported Codex behavior.
    public async Task Registration_IsOptInAndIdempotent()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddRuniqServer(options => options.AddAgent(new Agent("codex", "Codex", "instructions").UseCodex()));
        await using (var baseline = services.BuildServiceProvider())
        {
            await using var scope = baseline.CreateAsyncScope();
            Assert.Equal("AgentExecutorNotSupported", (await scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>().ExecuteAsync("codex", "input")).ErrorCode);
        }
        services.AddRuniqCodexExecutor(_ => { });
        services.AddRuniqCodexExecutor(_ => { });
        await using var enabled = services.BuildServiceProvider();
        await using var enabledScope = enabled.CreateAsyncScope();
        Assert.Equal(new[] { AgentExecutorKind.Model, AgentExecutorKind.Codex }, enabledScope.ServiceProvider.GetServices<IAgentExecutor>().Select(x => x.Kind));
    }

    [Fact]
    // Verifies a newly created session cannot be resumed until its original invocation has been disposed.
    public async Task NewSession_ResumptionIsBlockedWhileInitialRunIsActive()
    {
        var factory = new FakeFactory(() => new FakeProcess(Start + Message, wait: true));
        await using var services = Services(factory);
        await using var scope = services.CreateAsyncScope();
        var runtime = scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>();
        await using var stream = runtime.ExecuteStreamAsync("codex", "input").GetAsyncEnumerator();
        Assert.True(await stream.MoveNextAsync());
        var result = await runtime.ExecuteAsync("codex", new AgentQuery("follow-up") { ProviderSessionId = stream.Current.ProviderSessionId });
        Assert.Equal("CodexSessionBusy", result.ErrorCode);
        Assert.Single(factory.Processes);
    }

    [Fact]
    // Verifies pre-cancellation starts no process and cancellation reaches a stalled streaming consumer immediately.
    public async Task Cancellation_DoesNotDependOnConsumerAdvancing()
    {
        var factory = new FakeFactory(() => new FakeProcess(Start + Message, wait: true));
        await using var services = Services(factory);
        await using var scope = services.CreateAsyncScope();
        var runtime = scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAsync<AgentRunCanceledException>(() => runtime.ExecuteAsync("codex", "input", cancelled.Token));
        Assert.Empty(factory.Processes);
        using var cancellation = new CancellationTokenSource();
        await using var stream = runtime.ExecuteStreamAsync("codex", "input", cancellationToken: cancellation.Token).GetAsyncEnumerator();
        Assert.True(await stream.MoveNextAsync());
        cancellation.Cancel();
        Assert.True(factory.Processes[0].Token.IsCancellationRequested);
        await Assert.ThrowsAsync<AgentRunCanceledException>(async () => await stream.MoveNextAsync());
        Assert.Equal(1, factory.Processes[0].DisposeCount);
    }

    [Fact]
    // Verifies unsupported RAG overrides and oversized prompts fail without silently dropping configuration.
    public async Task UnsupportedInput_DoesNotStartProcess()
    {
        var factory = new FakeFactory(() => new FakeProcess(""));
        await using var services = Services(factory, options => options.MaxEventCharacters = 128);
        await using var scope = services.CreateAsyncScope();
        var runtime = scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>();
        Assert.Equal("CodexCapabilityNotSupported", (await runtime.ExecuteAsync("codex", new AgentQuery("input") { IndexName = "index" })).ErrorCode);
        Assert.Equal("CodexInputLimitExceeded", (await runtime.ExecuteAsync("codex", new string('x', 129))).ErrorCode);
        Assert.Empty(factory.Processes);
    }

    [Fact]
    // Verifies the hosted JSON response exposes the confirmed provider identity without changing request authorization.
    public async Task HostedResponse_PreservesProviderSession()
    {
        await using var services = Services(new FakeFactory(() => new FakeProcess(Start + Message + End)));
        await using var scope = services.CreateAsyncScope();
        var context = new Microsoft.AspNetCore.Http.DefaultHttpContext { RequestServices = scope.ServiceProvider };
        var response = await scope.ServiceProvider.GetRequiredService<AgentChatApiHandler>().ChatAsync("codex",
            new AgentChatRequest("input", AgentChatResponseMode.Result), context, CancellationToken.None);
        var value = Assert.IsAssignableFrom<Microsoft.AspNetCore.Http.IValueHttpResult>(response).Value;
        var dto = Assert.IsType<AgentChatResponse>(value);
        Assert.Equal(Session, dto.ProviderSessionId);
        Assert.Contains("providerSessionId", JsonSerializer.Serialize(dto, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    [Fact]
    // Verifies the model executor rejects a continuation key instead of silently starting an unrelated model conversation.
    public async Task ModelExecutor_RejectsContinuationWithoutProviderCall()
    {
        var factory = new FakeFactory(() => new FakeProcess(""));
        await using var services = Services(factory);
        await using var scope = services.CreateAsyncScope();
        var agent = new Agent("model", "Model", "instructions").UseModel("openai/gpt-5", "unused-key");
        var result = await scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>()
            .ExecuteAsync(agent, new AgentQuery("input") { ProviderSessionId = Session });
        Assert.Equal("AgentSessionNotSupported", result.ErrorCode);
        Assert.Empty(factory.Processes);
    }

    private static ServiceProvider Services(FakeFactory factory, Action<CodexExecutorOptions>? configure = null)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddRuniqServer(options => options.AddAgent(new Agent("codex", "Codex", "instructions").UseCodex()));
        services.AddRuniqCodexExecutor(options =>
        {
            options.WorkingDirectory = Path.GetTempPath();
            // This existing native binary is only a validation fixture; the fake factory never starts it.
            options.ExecutablePath = Environment.ProcessPath!;
            configure?.Invoke(options);
        });
        services.Replace(ServiceDescriptor.Singleton<ICodexProcessFactory>(factory));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private sealed class FakeFactory(Func<FakeProcess> create) : ICodexProcessFactory
    {
        internal List<FakeProcess> Processes { get; } = [];
        internal ProcessStartInfo? Command { get; private set; }
        public ICodexProcess Start(ProcessStartInfo startInfo, CancellationToken cancellationToken)
        {
            Command = startInfo;
            var process = create();
            process.Token = cancellationToken;
            Processes.Add(process);
            return process;
        }
    }

    private sealed class FakeProcess(string output, int exitCode = 0, string stderr = "", bool wait = false) : ICodexProcess
    {
        public TextReader StandardOutput { get; } = new ControlledReader(output, wait);
        public TextReader StandardError { get; } = new StringReader(stderr);
        internal string Input { get; private set; } = "";
        internal CancellationToken Token { get; set; }
        internal int DisposeCount { get; private set; }
        public Task WriteInputAsync(string input, CancellationToken cancellationToken) { Input = input; return Task.CompletedTask; }
        public Task<int> WaitForExitAsync(CancellationToken cancellationToken) => Task.FromResult(exitCode);
        public ValueTask DisposeAsync() { DisposeCount++; StandardOutput.Dispose(); StandardError.Dispose(); return ValueTask.CompletedTask; }
    }

    private sealed class ControlledReader(string text, bool wait) : StringReader(text)
    {
        public override async ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken cancellationToken = default)
        {
            var count = await base.ReadAsync(buffer, cancellationToken);
            if (count == 0 && wait) await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return count;
        }
    }
}

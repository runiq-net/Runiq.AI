using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Runiq.AI.Agents.Configuration;
using Runiq.AI.Agents.Runtime;
using Runiq.AI.Agents.Runtime.Cli;
using Runiq.AI.Agents.Runtime.Codex;
using Runiq.AI.Agents.Tools;
using Runiq.AI.Core;
using Runiq.AI.Core.Agents;
using Runiq.AI.LocalCliAgents.Agents;

namespace Runiq.AI.Agents.Tests.Agents;

public sealed class CodexToolBridgeTests : CliToolBridgeTests { protected override bool Claude => false; }
public sealed class ClaudeToolBridgeTests : CliToolBridgeTests { protected override bool Claude => true; }

public abstract class CliToolBridgeTests
{
    protected abstract bool Claude { get; }
    private string Kind => Claude ? "Claude" : "Codex";
    [Fact]
    // Verifies the sample's actual tool returns the documented totals through runtime, HTTP MCP and fake CLI output.
    public async Task SampleTool_ExecutesEndToEnd()
    {
        var factory = new ProtocolFactory(async (command, ct) =>
        {
            await using var client = await ConnectAsync(command, ct);
            var result = await client.CallToolAsync("change_summary", new Dictionary<string, object?>
            {
                ["files"] = new[] { new { path = "OrderService.cs", added = 45, deleted = 12 },
                    new { path = "OrderController.cs", added = 18, deleted = 4 }, new { path = "OrderServiceTests.cs", added = 90, deleted = 0 } }
            }, cancellationToken: ct);
            Assert.False(result.IsError);
            return Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        });
        await using var provider = Services(factory, agent: Claude ? new Agent("quick-project-assistant", "Quick", "Use tools").UseClaude(claude => claude.Model = "sonnet").AddTool<Runiq.AI.LocalCliAgents.Tools.ChangeSummaryTool>() : QuickProjectAssistant.Create());
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>().ExecuteAsync("quick-project-assistant", "Summarize these changes");
        Assert.Null(factory.Failure);
        Assert.True(result.IsSuccess, result.ErrorMessage);
        using var json = JsonDocument.Parse(result.Message!);
        Assert.Equal(3, json.RootElement.GetProperty("files").GetInt32());
        Assert.Equal(153, json.RootElement.GetProperty("added").GetInt32());
        Assert.Equal(16, json.RootElement.GetProperty("deleted").GetInt32());
    }

    [Fact]
    // Verifies the actual runtime exports a typed tool, invokes scoped dependencies, publishes dashboard events and resumes with a fresh bridge.
    public async Task Runtime_InvokesToolAndResumesWithFreshCredentials()
    {
        var endpoints = new List<string>();
        var secrets = new List<string>();
        var factory = new ProtocolFactory(async (command, ct) =>
        {
            var endpoint = Endpoint(command);
            endpoints.Add(endpoint.AbsoluteUri);
            secrets.Add(command.Environment[CliToolBridge.TokenVariable]!);
            Assert.DoesNotContain(secrets[^1], string.Join(" ", command.ArgumentList));
            using var http = new HttpClient();
            Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync(endpoint, ct)).StatusCode);
            await using var client = await ConnectAsync(command, ct);
            var tool = Assert.Single(await client.ListToolsAsync(cancellationToken: ct));
            Assert.Equal("echo", tool.Name);
            Assert.Contains("value", tool.JsonSchema.GetRawText(), StringComparison.OrdinalIgnoreCase);
            var result = await client.CallToolAsync("echo", new Dictionary<string, object?> { ["value"] = "bridge-output" }, cancellationToken: ct);
            Assert.False(result.IsError);
            return Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        });
        await using var provider = Services(factory);
        await using var scope = provider.CreateAsyncScope();
        var runtime = scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>();
        var events = new List<AgentExecutionEvent>();
        await foreach (var item in runtime.ExecuteStreamAsync("agent", "Use echo")) events.Add(item);
        Assert.Null(factory.Failure);
        Assert.Equal(new[] { AgentExecutionEventKind.ToolCallStarted, AgentExecutionEventKind.ToolCallCompleted,
            AgentExecutionEventKind.AssistantDelta, AgentExecutionEventKind.Completed }, events.Select(e => e.Kind));
        Assert.Equal(events[0].ToolCallId, events[1].ToolCallId);
        Assert.Equal("echo", AgentChatStreamEventMapper.FromExecutionEvent(events[0]).ToolName);
        Assert.Contains("bridge-output", events[^1].Message);
        var followUp = await runtime.ExecuteAsync("agent", new AgentQuery("Use echo again") { ProviderSessionId = events[^1].ProviderSessionId });
        Assert.True(followUp.IsSuccess, followUp.ErrorMessage);
        Assert.Null(factory.Failure);
        Assert.Equal(2, scope.ServiceProvider.GetRequiredService<InvocationProbe>().Calls);
        Assert.NotEqual(secrets[0], secrets[1]);
        Assert.Single(factory.Commands, c => c.ArgumentList.Contains(Claude ? "--resume" : "resume"));
        if (!Claude) Assert.All(factory.Commands, c => Assert.Contains("test-model", c.ArgumentList));
        else Assert.All(factory.Commands, c =>
        {
            Assert.Equal("mcp__runiq_agent_tools__echo", c.ArgumentList[c.ArgumentList.IndexOf("--allowedTools") + 1]);
            Assert.Contains("dontAsk", c.ArgumentList);
            Assert.DoesNotContain("--dangerously-skip-permissions", c.ArgumentList);
            using var config = JsonDocument.Parse(c.ArgumentList[c.ArgumentList.IndexOf("--mcp-config") + 1]);
            Assert.Equal("Bearer ${RUNIQ_CLI_TOOL_TOKEN}", config.RootElement.GetProperty("mcpServers")
                .GetProperty("runiq_agent_tools").GetProperty("headers").GetProperty("Authorization").GetString());
        });
        using var after = new HttpClient();
        foreach (var endpoint in endpoints)
            await Assert.ThrowsAsync<HttpRequestException>(() => after.GetAsync(endpoint));
    }

    [Theory]
    [InlineData("missing", "ok", "ToolNotFound")]
    [InlineData("echo", "throw", "ToolExecutionFailed")]
    [InlineData("echo", 42, "ToolInputInvalid")]
    // Verifies tool failures return MCP errors and existing dashboard failure events without terminating the whole conversation.
    public async Task Runtime_ReturnsToolFailuresToCodex(string name, object value, string code)
    {
        var factory = new ProtocolFactory(async (command, ct) =>
        {
            await using var client = await ConnectAsync(command, ct);
            var result = await client.CallToolAsync(name, new Dictionary<string, object?> { ["value"] = value }, cancellationToken: ct);
            Assert.True(result.IsError);
            Assert.Contains(code, Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text);
            return "Tool failure handled";
        });
        await using var provider = Services(factory);
        await using var scope = provider.CreateAsyncScope();
        var events = new List<AgentExecutionEvent>();
        await foreach (var item in scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>().ExecuteStreamAsync("agent", "Use tool")) events.Add(item);
        Assert.Null(factory.Failure);
        Assert.Equal(code, Assert.Single(events, e => e.Kind == AgentExecutionEventKind.ToolCallFailed).ErrorCode);
        Assert.Equal(AgentExecutionEventKind.Completed, events[^1].Kind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    // Verifies cancellation and the executor timeout stop an active tool before releasing its run scope and listener.
    public async Task Runtime_CancelsActiveToolAndClosesBridge(bool timeout)
    {
        var factory = new ProtocolFactory(async (command, ct) =>
        {
            await using var client = await ConnectAsync(command, ct);
            await client.CallToolAsync("echo", new Dictionary<string, object?> { ["value"] = "wait" }, cancellationToken: ct);
            return "unexpected";
        });
        await using var provider = Services(factory, timeout ? TimeSpan.FromSeconds(2) : null);
        await using var scope = provider.CreateAsyncScope();
        using var cancellation = new CancellationTokenSource();
        var probe = scope.ServiceProvider.GetRequiredService<InvocationProbe>();
        var execution = scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>().ExecuteAsync("agent", "wait", cancellation.Token);
        await probe.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        if (!timeout) cancellation.Cancel();
        if (timeout) Assert.Equal(Kind + "Timeout", (await execution.WaitAsync(TimeSpan.FromSeconds(10))).ErrorCode);
        else await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution);
        Assert.True(probe.Stopped);
        using var http = new HttpClient();
        await Assert.ThrowsAsync<HttpRequestException>(() => http.GetAsync(Endpoint(Assert.Single(factory.Commands))));
    }

    [Fact]
    // Verifies simultaneous runs cannot authenticate to each other's listener and resolve distinct scoped dependencies.
    public async Task ConcurrentRuns_IsolateCredentialsAndScopes()
    {
        var commands = new System.Collections.Concurrent.ConcurrentBag<ProcessStartInfo>();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var factory = new ProtocolFactory(async (command, ct) =>
        {
            commands.Add(command);
            if (commands.Count == 2) ready.TrySetResult();
            await ready.Task.WaitAsync(ct);
            var other = commands.Single(c => !ReferenceEquals(c, command));
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization = new("Bearer", other.Environment[CliToolBridge.TokenVariable]);
            Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync(Endpoint(command), ct)).StatusCode);
            await using var client = await ConnectAsync(command, ct);
            var name = Assert.Single(await client.ListToolsAsync(cancellationToken: ct)).Name;
            var denied = await client.CallToolAsync(name == "echo" ? "other" : "echo", new Dictionary<string, object?> { ["value"] = "scope" }, cancellationToken: ct);
            Assert.True(denied.IsError);
            var result = await client.CallToolAsync(name, new Dictionary<string, object?> { ["value"] = "scope" }, cancellationToken: ct);
            return Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        });
        await using var provider = Services(factory);
        await using var first = provider.CreateAsyncScope();
        await using var second = provider.CreateAsyncScope();
        var results = await Task.WhenAll(first.ServiceProvider.GetRequiredService<AgentExecutionRuntime>().ExecuteAsync("agent", "scope"),
            second.ServiceProvider.GetRequiredService<AgentExecutionRuntime>().ExecuteAsync("other", "scope"));
        Assert.Null(factory.Failure);
        Assert.All(results, r => Assert.True(r.IsSuccess, r.ErrorMessage));
        Assert.NotEqual(results[0].Message, results[1].Message);
    }

    [Fact]
    // Verifies disposing an unfinished stream cancels the tool and releases the listener before the caller's scope ends.
    public async Task AbandonedStream_CleansUpToolAndListener()
    {
        var factory = new ProtocolFactory(async (command, ct) =>
        {
            await using var client = await ConnectAsync(command, ct);
            await client.CallToolAsync("echo", new Dictionary<string, object?> { ["value"] = "wait" }, cancellationToken: ct);
            return "unexpected";
        });
        await using var provider = Services(factory);
        await using var scope = provider.CreateAsyncScope();
        var probe = scope.ServiceProvider.GetRequiredService<InvocationProbe>();
        var stream = scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>().ExecuteStreamAsync("agent", "wait").GetAsyncEnumerator();
        try
        {
            Assert.True(await stream.MoveNextAsync());
            Assert.Equal(AgentExecutionEventKind.ToolCallStarted, stream.Current.Kind);
            await probe.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally { await stream.DisposeAsync(); }
        Assert.True(probe.Stopped);
        using var http = new HttpClient();
        await Assert.ThrowsAsync<HttpRequestException>(() => http.GetAsync(Endpoint(Assert.Single(factory.Commands))));
    }

    [Fact]
    // Verifies a failed CLI process still closes its tool bridge and preserves the existing process error mapping.
    public async Task ProcessFailure_ClosesToolBridge()
    {
        var factory = new ProtocolFactory(async (command, ct) =>
        {
            await using var client = await ConnectAsync(command, ct);
            Assert.Single(await client.ListToolsAsync(cancellationToken: ct));
            return "provisional";
        }, exitCode: 1);
        await using var provider = Services(factory);
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>().ExecuteAsync("agent", "hello");
        Assert.Null(factory.Failure);
        Assert.Equal(Kind + "ProcessFailed", result.ErrorCode);
        using var http = new HttpClient();
        await Assert.ThrowsAsync<HttpRequestException>(() => http.GetAsync(Endpoint(Assert.Single(factory.Commands))));
    }

    [Fact]
    // Verifies oversized tool output is converted to a bounded tool error instead of entering the event stream or CLI context.
    public async Task ToolOutput_EnforcesConfiguredLimit()
    {
        var factory = new ProtocolFactory(async (command, ct) =>
        {
            await using var client = await ConnectAsync(command, ct);
            var result = await client.CallToolAsync("echo", new Dictionary<string, object?> { ["value"] = "large" }, cancellationToken: ct);
            Assert.True(result.IsError);
            var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
            Assert.Contains("ToolOutputTooLarge", text);
            Assert.True(text.Length < 256);
            return "limit handled";
        });
        await using var provider = Services(factory);
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>().ExecuteAsync("agent", "large");
        Assert.Null(factory.Failure);
        Assert.True(result.IsSuccess, result.ErrorMessage);
    }

    private ServiceProvider Services(ProtocolFactory factory, TimeSpan? timeout = null, Agent? agent = null)
    {
        factory.Claude = Claude;
        var services = new ServiceCollection().AddLogging();
        services.AddScoped<InvocationProbe>();
        services.AddRuniqServer(o =>
        {
            o.AddAgent(agent ?? Select(new Agent("agent", "Agent", "Use tools")).AddTool<EchoTool>());
            o.AddAgent(Select(new Agent("other", "Other", "Use tools")).AddTool<OtherTool>());
        });
        services.Configure<CodexExecutorOptions>(o =>
        {
            o.ExecutablePath = Environment.ProcessPath!;
            o.WorkingDirectory = Path.GetTempPath();
            o.Timeout = timeout ?? TimeSpan.FromSeconds(20);
            o.MaxEventCharacters = 4096;
            o.MaxOutputCharacters = 8192;
        });
        services.Configure<ClaudeExecutorOptions>(o =>
        {
            o.ExecutablePath = Environment.ProcessPath!;
            o.WorkingDirectory = Path.GetTempPath();
            o.Timeout = timeout ?? TimeSpan.FromSeconds(20);
            o.MaxEventCharacters = 4096;
            o.MaxOutputCharacters = 8192;
        });
        services.Replace(ServiceDescriptor.Singleton<ICliProcessFactory>(factory));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private Agent Select(Agent agent) => Claude ? agent.UseClaude(claude => claude.Model = "sonnet") : agent.UseCodex(o => o.Model = "test-model");

    private static Uri Endpoint(ProcessStartInfo command)
    {
        if (command.ArgumentList.Contains("--mcp-config"))
        {
            using var json = JsonDocument.Parse(command.ArgumentList[command.ArgumentList.IndexOf("--mcp-config") + 1]);
            return new Uri(json.RootElement.GetProperty("mcpServers").GetProperty("runiq_agent_tools").GetProperty("url").GetString()!);
        }
        var setting = command.ArgumentList.Single(a => a.StartsWith("mcp_servers.runiq_agent_tools="));
        return new Uri(setting.Split('"')[1]);
    }

    private static Task<McpClient> ConnectAsync(ProcessStartInfo command, CancellationToken ct) => McpClient.CreateAsync(
        new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = Endpoint(command),
            AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = "Bearer " + command.Environment[CliToolBridge.TokenVariable] }
        }), cancellationToken: ct);

    public sealed class InvocationProbe
    {
        public string Id { get; } = Guid.NewGuid().ToString();
        public int Calls { get; set; }
        public bool Stopped { get; set; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public sealed record EchoInput(string Value);

    [RuniqTool("other", "Returns the current run scope identity.")]
    public sealed class OtherTool(InvocationProbe probe) : IRuniqTool<EchoInput, string>
    {
        public Task<string> ExecuteAsync(EchoInput input, CancellationToken cancellationToken = default) => Task.FromResult(probe.Id);
    }

    [RuniqTool("echo", "Echoes a value using the current run scope.")]
    public sealed class EchoTool(InvocationProbe probe) : IRuniqTool<EchoInput, string>
    {
        public async Task<string> ExecuteAsync(EchoInput input, CancellationToken cancellationToken = default)
        {
            probe.Calls++;
            probe.Started.TrySetResult();
            try
            {
                if (input.Value == "wait") await Task.Delay(Timeout.Infinite, cancellationToken);
                if (input.Value == "throw") throw new InvalidOperationException("private diagnostic");
                if (input.Value == "large") return new string('x', 5000);
                return input.Value == "scope" ? probe.Id : input.Value;
            }
            finally { probe.Stopped = true; }
        }
    }

    private sealed class ProtocolFactory(Func<ProcessStartInfo, CancellationToken, Task<string>> exchange, int exitCode = 0) : ICliProcessFactory
    {
        internal System.Collections.Concurrent.ConcurrentBag<ProcessStartInfo> Commands { get; } = [];
        internal bool Claude { get; set; }
        internal Exception? Failure { get; private set; }
        public ICliProcess Start(ProcessStartInfo command, CancellationToken ct)
        {
            Commands.Add(command);
            return new ProtocolProcess(async token =>
            {
                try { return await exchange(command, token); }
                catch (Exception ex) { Failure = ex; throw; }
            }, command.ArgumentList.FirstOrDefault(a => Guid.TryParse(a, out _)) ?? Guid.NewGuid().ToString(), exitCode, Claude);
        }
    }

    private sealed class ProtocolProcess(Func<CancellationToken, Task<string>> exchange, string session, int exitCode, bool claude) : ICliProcess
    {
        private readonly TaskCompletionSource<string> output = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TextReader StandardOutput => reader ??= new DeferredReader(output.Task);
        private TextReader? reader;
        public TextReader StandardError { get; } = new StringReader("");
        public async Task WriteInputAsync(string input, CancellationToken ct)
        {
            try
            {
                var text = await exchange(ct);
                if (claude)
                {
                    output.TrySetResult(JsonSerializer.Serialize(new { type = "system", subtype = "init", session_id = session,
                        mcp_servers = new[] { new { name = "runiq_agent_tools", status = "connected" } } }) + "\n" +
                        JsonSerializer.Serialize(new { type = "result", subtype = "success", is_error = false, session_id = session, result = text }) + "\n");
                    return;
                }
                output.TrySetResult($"{{\"type\":\"thread.started\",\"thread_id\":\"{session}\"}}\n{{\"type\":\"turn.started\"}}\n" +
                    JsonSerializer.Serialize(new { type = "item.completed", item = new { id = "1", type = "agent_message", text } }) + "\n{\"type\":\"turn.completed\"}\n");
            }
            catch (Exception ex) { output.TrySetException(ex); }
        }
        public Task<int> WaitForExitAsync(CancellationToken ct) => Task.FromResult(exitCode);
        public ValueTask DisposeAsync() { reader?.Dispose(); StandardError.Dispose(); return ValueTask.CompletedTask; }
    }

    private sealed class DeferredReader(Task<string> output) : TextReader
    {
        private StringReader? reader;
        public override async ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken cancellationToken = default)
        {
            reader ??= new StringReader(await output.WaitAsync(cancellationToken));
            return await reader.ReadAsync(buffer, cancellationToken);
        }
        protected override void Dispose(bool disposing) { if (disposing) reader?.Dispose(); base.Dispose(disposing); }
    }
}

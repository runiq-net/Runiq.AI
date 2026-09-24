using Runiq.AI.Agents.Runtime.Cli;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Runiq.AI.Agents.Configuration;
using Runiq.AI.Agents.Tools;

namespace Runiq.AI.Agents.Runtime.Claude;

internal sealed class ClaudeAgentExecutor(ICliProcessFactory processes, IOptions<ClaudeExecutorOptions> options,
    ClaudeSessionGate sessions, ILogger<ClaudeAgentExecutor> logger) : IAgentExecutor
{
    public AgentExecutorKind Kind => AgentExecutorKind.Claude;

    public async IAsyncEnumerable<AgentExecutionEvent> ExecuteAsync(AgentExecutionRequest request, AgentRunContext run,
        AgentToolInvoker toolInvoker, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var events = ExecuteCoreAsync(request, run, toolInvoker, cancellationToken).GetAsyncEnumerator(cancellationToken);
        while (true)
        {
            AgentExecutionEvent? next;
            try
            {
                if (!await events.MoveNextAsync()) yield break;
                next = events.Current;
            }
            catch (CliProcessException exception)
            {
                next = AgentExecutionEvent.Failed(FailureMessage("Claude" + exception.Code), "Claude" + exception.Code);
            }
            catch (ClaudeException exception)
            {
                logger.LogWarning("Claude execution ended with {ErrorCode} for run {RunId}.", exception.Code, run.RunId);
                next = AgentExecutionEvent.Failed(FailureMessage(exception.Code), exception.Code);
            }
            yield return next;
            if (next.Status != AgentRunStatus.Running) yield break;
        }
    }

    private async IAsyncEnumerable<AgentExecutionEvent> ExecuteCoreAsync(AgentExecutionRequest request, AgentRunContext run, AgentToolInvoker toolInvoker,
        [EnumeratorCancellation] CancellationToken callerToken)
    {
        callerToken.ThrowIfCancellationRequested();
        if (request.Agent.Rag is { Enabled: true } || request.Query.IndexName is not null)
            throw new ClaudeException("ClaudeCapabilityNotSupported");
        var configuration = options.Value;
        var sessionId = request.Query.ProviderSessionId;
        var command = ClaudeCommand.Create(configuration, sessionId);
        var prompt = $"Agent instructions:\n{request.Agent.Instructions}\n\nUser request:\n{request.Query.Message}";
        if (prompt.Length > configuration.MaxEventCharacters) throw new ClaudeException("ClaudeInputLimitExceeded");
        if (sessionId is not null && !sessions.TryEnter(sessionId.ToLowerInvariant()))
            throw new ClaudeException("ClaudeSessionBusy");
        var lockedSession = sessionId?.ToLowerInvariant();
        try
        {
            using var timeout = new CancellationTokenSource(configuration.Timeout);
            using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(callerToken, timeout.Token);
            await using var events = RunWithToolsAsync(request.Agent, toolInvoker, command, prompt, configuration, sessionId, run, lifetime, ConfirmSession)
                .GetAsyncEnumerator(lifetime.Token);
            while (true)
            {
                AgentExecutionEvent next;
                try
                {
                    if (!await events.MoveNextAsync()) yield break;
                    next = events.Current;
                }
                catch (Exception exception) when (exception is OperationCanceledException or IOException or ObjectDisposedException)
                {
                    callerToken.ThrowIfCancellationRequested();
                    throw new ClaudeException(timeout.IsCancellationRequested ? "ClaudeTimeout" : "ClaudeProcessIoFailed");
                }
                callerToken.ThrowIfCancellationRequested();
                if (timeout.IsCancellationRequested) throw new ClaudeException("ClaudeTimeout");
                yield return next;
            }
        }
        finally
        {
            if (lockedSession is not null) sessions.Exit(lockedSession);
        }

        void ConfirmSession(string confirmed)
        {
            if (lockedSession is null)
            {
                if (!sessions.TryEnter(confirmed.ToLowerInvariant())) throw new ClaudeException("ClaudeSessionBusy");
                lockedSession = confirmed.ToLowerInvariant();
            }
            run.SetProviderSessionId(confirmed);
        }
    }

    private async IAsyncEnumerable<AgentExecutionEvent> RunWithToolsAsync(Agent agent, AgentToolInvoker invoker,
        System.Diagnostics.ProcessStartInfo command, string prompt, ClaudeExecutorOptions configuration,
        string? sessionId, AgentRunContext run, CancellationTokenSource lifetime, Action<string> confirmSession)
    {
        if (agent.Tools.Count == 0)
        {
            await foreach (var item in RunProcessAsync(command, prompt, configuration, sessionId, run, lifetime, confirmSession))
                yield return item;
            yield break;
        }

        var channel = Channel.CreateBounded<AgentExecutionEvent>(new BoundedChannelOptions(32)
        {
            SingleReader = true, FullMode = BoundedChannelFullMode.Wait
        });
        await using var bridge = await ClaudeToolBridge.StartAsync(agent, invoker, command, configuration, channel.Writer, lifetime.Token);
        var pump = PumpAsync();
        try
        {
            // Process cleanup cancels lifetime even on success; drain already-published events before observing completion.
            await foreach (var item in channel.Reader.ReadAllAsync()) yield return item;
        }
        finally
        {
            await lifetime.CancelAsync();
            await pump;
        }

        async Task PumpAsync()
        {
            Exception? failure = null;
            try
            {
                await foreach (var item in RunProcessAsync(command, prompt, configuration, sessionId, run, lifetime, confirmSession))
                    await channel.Writer.WriteAsync(item, lifetime.Token);
            }
            catch (Exception exception) { failure = exception; }
            finally { channel.Writer.TryComplete(failure); }
        }
    }

    private async IAsyncEnumerable<AgentExecutionEvent> RunProcessAsync(System.Diagnostics.ProcessStartInfo command,
        string prompt, ClaudeExecutorOptions configuration, string? sessionId, AgentRunContext run,
        CancellationTokenSource lifetime, Action<string> confirmSession)
    {
        ICliProcess process;
        try { process = processes.Start(command, lifetime.Token); }
        catch (Win32Exception exception) when (exception.NativeErrorCode is 2 or 3)
        { throw new ClaudeException("ClaudeExecutableNotFound"); }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or UnauthorizedAccessException)
        { throw new ClaudeException("ClaudeProcessStartFailed"); }

        Task<string> stderr = Task.FromResult(string.Empty);
        Task input = Task.CompletedTask;
        try
        {
            stderr = CliOutputReader.DrainErrorAsync(process.StandardError, lifetime.Token);
            input = process.WriteInputAsync(prompt, lifetime.Token);
            var protocol = new ClaudeJsonProtocol(sessionId, command.ArgumentList.Contains("--mcp-config"));
            await foreach (var line in CliOutputReader.ReadLinesAsync(process.StandardOutput,
                               configuration.MaxEventCharacters, configuration.MaxOutputCharacters, lifetime.Token))
            {
                var mapped = protocol.Apply(line);
                if (protocol.SessionId is not null) confirmSession(protocol.SessionId);
                if (mapped is not null) yield return mapped;
            }
            var exitCode = await process.WaitForExitAsync(lifetime.Token);
            var diagnostic = await stderr;
            // A completed JSON event is provisional until the actual process exits successfully.
            if (exitCode != 0)
            {
                logger.LogWarning("Claude exited with code {ExitCode} for run {RunId}.", exitCode, run.RunId);
                throw new ClaudeException(protocol.FailureCode ?? ClaudeJsonProtocol.ClassifyFailure(diagnostic));
            }
            await input;
            if (!protocol.Completed) throw new ClaudeException(protocol.FailureCode ?? "ClaudeOutputInvalid");
            yield return AgentExecutionEvent.Completed();
        }
        finally
        {
            // The linked lifetime belongs to this enumeration; disposal and abandoned streams stop child work.
            await lifetime.CancelAsync();
            try { await process.DisposeAsync(); }
            finally
            {
                await ObservePumpAsync(input);
                await ObservePumpAsync(stderr);
            }
        }
    }

    private static async Task ObservePumpAsync(Task task)
    {
        try { await task; }
        catch (Exception exception) when (exception is OperationCanceledException or IOException or ObjectDisposedException)
        { /* Shutdown closes redirected pipes; the primary execution outcome owns the error. */ }
    }

    private static string FailureMessage(string code) => code switch
    {
        "ClaudeNotInstalled" => "Claude was not found on PATH. Install the CLI or configure its native executable path.",
        "ClaudeExecutableNotFound" => "The configured Claude executable was not found.",
        "ClaudeAuthenticationFailed" => "Claude authentication failed. Sign in using the CLI under the host account.",
        "ClaudeConfigurationInvalid" => "Claude configuration is invalid. Check the workspace and CLI settings.",
        "ClaudeProcessStartFailed" => "The Claude process could not be started.",
        "ClaudeTimeout" => "Claude exceeded the configured execution timeout.",
        "ClaudeOutputInvalid" => "Claude returned malformed or incomplete execution output.",
        "ClaudeOutputLimitExceeded" => "Claude output exceeded the configured limit.",
        "ClaudeInputLimitExceeded" => "The Claude prompt exceeded the configured limit.",
        "ClaudeSessionInvalid" => "The Claude session identifier is invalid or differs from the requested session.",
        "ClaudeSessionNotFound" => "The requested Claude session was not found.",
        "ClaudeSessionBusy" => "The Claude session is already executing in this host.",
        "ClaudeCapabilityNotSupported" => "The Claude CLI executor does not support Runiq RAG bindings.",
        "ClaudeToolBridgeFailed" => "Claude could not connect to the local Runiq tool bridge. Check CLI MCP support and local networking.",
        "ClaudeProcessIoFailed" => "Communication with the Claude process failed.",
        "ClaudePlatformNotSupported" => "The local Claude process adapter currently supports Windows and Linux hosts.",
        _ => "Claude execution failed. Check the CLI installation and its local configuration."
    };
}

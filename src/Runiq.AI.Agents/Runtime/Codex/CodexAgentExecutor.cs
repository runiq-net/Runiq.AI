using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Runiq.AI.Agents.Configuration;
using Runiq.AI.Agents.Tools;

namespace Runiq.AI.Agents.Runtime.Codex;

internal sealed class CodexAgentExecutor(ICodexProcessFactory processes, IOptions<CodexExecutorOptions> options,
    CodexSessionGate sessions, ILogger<CodexAgentExecutor> logger) : IAgentExecutor
{
    public AgentExecutorKind Kind => AgentExecutorKind.Codex;

    public async IAsyncEnumerable<AgentExecutionEvent> ExecuteAsync(AgentExecutionRequest request, AgentRunContext run,
        AgentToolInvoker toolInvoker, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var events = ExecuteCoreAsync(request, run, cancellationToken).GetAsyncEnumerator(cancellationToken);
        while (true)
        {
            AgentExecutionEvent? next;
            try
            {
                if (!await events.MoveNextAsync()) yield break;
                next = events.Current;
            }
            catch (CodexException exception)
            {
                logger.LogWarning("Codex execution ended with {ErrorCode} for run {RunId}.", exception.Code, run.RunId);
                next = AgentExecutionEvent.Failed(FailureMessage(exception.Code), exception.Code);
            }
            yield return next;
            if (next.Status != AgentRunStatus.Running) yield break;
        }
    }

    private async IAsyncEnumerable<AgentExecutionEvent> ExecuteCoreAsync(AgentExecutionRequest request, AgentRunContext run,
        [EnumeratorCancellation] CancellationToken callerToken)
    {
        callerToken.ThrowIfCancellationRequested();
        if (request.Agent.Tools.Count != 0 || request.Agent.Rag is { Enabled: true } || request.Query.IndexName is not null)
            throw new CodexException("CodexCapabilityNotSupported");
        var configuration = options.Value;
        var sessionId = request.Query.ProviderSessionId;
        var command = CodexCommand.Create(configuration, sessionId);
        var prompt = $"Agent instructions:\n{request.Agent.Instructions}\n\nUser request:\n{request.Query.Message}";
        if (prompt.Length > configuration.MaxEventCharacters) throw new CodexException("CodexInputLimitExceeded");
        if (sessionId is not null && !sessions.TryEnter(sessionId.ToLowerInvariant()))
            throw new CodexException("CodexSessionBusy");
        var lockedSession = sessionId?.ToLowerInvariant();
        try
        {
            using var timeout = new CancellationTokenSource(configuration.Timeout);
            using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(callerToken, timeout.Token);
            await using var events = RunProcessAsync(command, prompt, configuration, sessionId, run, lifetime, ConfirmSession)
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
                    throw new CodexException(timeout.IsCancellationRequested ? "CodexTimeout" : "CodexProcessIoFailed");
                }
                callerToken.ThrowIfCancellationRequested();
                if (timeout.IsCancellationRequested) throw new CodexException("CodexTimeout");
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
                if (!sessions.TryEnter(confirmed.ToLowerInvariant())) throw new CodexException("CodexSessionBusy");
                lockedSession = confirmed.ToLowerInvariant();
            }
            run.SetProviderSessionId(confirmed);
        }
    }

    private async IAsyncEnumerable<AgentExecutionEvent> RunProcessAsync(System.Diagnostics.ProcessStartInfo command,
        string prompt, CodexExecutorOptions configuration, string? sessionId, AgentRunContext run,
        CancellationTokenSource lifetime, Action<string> confirmSession)
    {
        ICodexProcess process;
        try { process = processes.Start(command, lifetime.Token); }
        catch (Win32Exception exception) when (exception.NativeErrorCode is 2 or 3)
        { throw new CodexException("CodexExecutableNotFound"); }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or UnauthorizedAccessException)
        { throw new CodexException("CodexProcessStartFailed"); }

        var stderr = CodexOutputReader.DrainErrorAsync(process.StandardError, lifetime.Token);
        var input = process.WriteInputAsync(prompt, lifetime.Token);
        try
        {
            var protocol = new CodexJsonProtocol(sessionId);
            await foreach (var line in CodexOutputReader.ReadLinesAsync(process.StandardOutput,
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
                logger.LogWarning("Codex exited with code {ExitCode} for run {RunId}.", exitCode, run.RunId);
                throw new CodexException(protocol.FailureCode ?? CodexJsonProtocol.ClassifyFailure(diagnostic));
            }
            await input;
            if (!protocol.Completed) throw new CodexException(protocol.FailureCode ?? "CodexOutputInvalid");
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
        "CodexNotInstalled" => "Codex was not found on PATH. Install the CLI or configure its native executable path.",
        "CodexExecutableNotFound" => "The configured Codex executable was not found.",
        "CodexAuthenticationFailed" => "Codex authentication failed. Sign in using the CLI under the host account.",
        "CodexConfigurationInvalid" => "Codex configuration is invalid. Check the workspace, sandbox and CLI configuration.",
        "CodexProcessStartFailed" => "The Codex process could not be started.",
        "CodexTimeout" => "Codex exceeded the configured execution timeout.",
        "CodexOutputInvalid" => "Codex returned malformed or incomplete execution output.",
        "CodexOutputLimitExceeded" => "Codex output exceeded the configured limit.",
        "CodexInputLimitExceeded" => "The Codex prompt exceeded the configured limit.",
        "CodexSessionInvalid" => "The Codex session identifier is invalid or differs from the requested session.",
        "CodexSessionNotFound" => "The requested Codex session was not found.",
        "CodexSessionBusy" => "The Codex session is already executing in this host.",
        "CodexCapabilityNotSupported" => "The Codex CLI executor does not support Runiq tool or RAG bindings.",
        "CodexProcessIoFailed" => "Communication with the Codex process failed.",
        "CodexPlatformNotSupported" => "The local Codex process adapter currently supports Windows and Linux hosts.",
        _ => "Codex execution failed. Check the CLI installation and its local configuration."
    };
}

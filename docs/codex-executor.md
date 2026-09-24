# Local Codex executor

The opt-in adapter implements the existing `IAgentExecutor`. It invokes the user's
installed CLI with `codex exec --json`, rather than making a separate model API
call. `ICodexProcessFactory` / `ICodexProcess` are internal OS-boundary test seams,
not another agent or coding-harness abstraction. Model and Claude registrations,
resolver duplicate detection, and the runtime lifecycle remain unchanged.

## Host configuration

```csharp
using Runiq.AI.Agents;
using Runiq.AI.Agents.Configuration;
using Runiq.AI.Agents.Runtime;
using Runiq.AI.Core;

builder.Services.AddRuniqServer(server => server.AddAgent(
    new Agent("reviewer", "Reviewer", "Review the repository and explain findings.").UseCodex()));
builder.Services.AddRuniqCodexExecutor(options =>
{
    options.WorkingDirectory = Path.GetFullPath("/srv/repository");
    options.Timeout = TimeSpan.FromMinutes(5);
    options.Sandbox = CodexSandboxMode.ReadOnly;
    // Optional absolute native executable path, for example C:\\Tools\\codex.exe.
    // options.ExecutablePath = "/usr/local/bin/codex";
});
```

Resolve `AgentExecutionRuntime` inside a DI scope. The compatibility constructors
that accept model clients still register only the model executor. Without
`AddRuniqCodexExecutor`, `UseCodex()` retains `AgentExecutorNotSupported`. Repeated
adapter registration is idempotent; adding another Codex executor remains a
duplicate-kind error. Registration does not run Codex or authenticate.

The workspace must be an existing absolute directory. By default it must also be
a Git repository; `SkipGitRepositoryCheck` is an explicit host opt-in. Configuration
belongs to the trusted host, not chat request input. One registration configures
one workspace for the host's Codex agents. Use separate host containers for different
workspace policies. The default sandbox is read-only; workspace-write is available.
Interactive approvals and sandbox bypass are not enabled.

The child inherits the existing CLI account, home and `CODEX_HOME` configuration.
The adapter never reads or copies credentials, calls login, or accepts an API key.
`OPENAI_API_KEY` and `CODEX_API_KEY` are removed from the child environment to avoid
accidentally using the host application's keys. Saved CLI authentication and custom
provider settings still belong to the user; this adapter does not convert an
API-key-authenticated CLI account into ChatGPT authentication.

Executable discovery uses absolute PATH entries. On Windows it uses native
`codex.exe`, including the standard npm optional native-package layout; it does
not execute `.cmd` or PowerShell shims. An explicit path avoids PATH ambiguity.
Prompts travel through stdin and arguments through `ProcessStartInfo.ArgumentList`.
No shell interprets user input. Agent instructions precede the user message in the
CLI prompt; CLI/project instructions retain their normal Codex precedence.

## Streaming, ownership and limits

Batch and streaming both consume the same JSONL path. Completed `agent_message`
items become `AssistantDelta` events as they arrive; this is message-granularity
streaming, not a promise of token deltas. Reasoning, native tool items and telemetry
are not represented as Runiq tool calls. Runiq tools and active RAG configurations
are explicitly rejected; Codex's own configured tools remain managed by Codex.

A `turn.completed` record is provisional until EOF and a zero exit code. Malformed,
duplicate or incomplete terminal output fails the run. Additive unknown event/item
types are ignored. Transient `error` events may precede a successful retry; a failed
turn or nonzero exit determines failure. Output limits bound individual JSONL
records and total stdout; stderr is drained concurrently with only 16 KiB retained
for internal classification. Raw stderr, prompts and protocol errors are not logged
or returned as error messages. Logs include safe error codes, exit codes and RunId.

The runtime still owns event sequence, timestamps, one terminal transition and
disposal before terminal publication. Caller cancellation throws
`AgentRunCanceledException`; it does not synthesize a cancelled event/result.
Timeout is a `Failed` run with `CodexTimeout`. Cancellation kills child work even
when the streaming consumer is paused. Dispose unfinished enumerators promptly.

Windows uses a kill-on-close Job Object; Linux uses a dedicated `setsid` process
group (util-linux must be installed). Parent exit also terminates remaining owned
descendants, preventing inherited output handles from keeping pipes open. Cleanup
waits up to ten seconds and reports failures through the existing runtime cleanup
contract. Current supported hosts are Windows and Linux; other OSes fail explicitly.
Linux containment covers ordinary descendants, not a malicious program deliberately
escaping its session or host termination by SIGKILL. This is resource ownership,
not an alternative security sandbox; use the Codex sandbox and trusted local hosts.

## Continuation

```csharp
var first = await runtime.ExecuteAsync("reviewer", "Review the change.", cancellationToken);
if (first.IsSuccess && first.ProviderSessionId is { } session)
{
    var next = await runtime.ExecuteAsync("reviewer",
        new AgentQuery("Explain the most important finding.") { ProviderSessionId = session },
        cancellationToken);
}
```

Only `thread.started.thread_id` confirms a session. It is propagated through
`AgentRunContext`, execution events/results and HTTP/SSE response metadata.
Each follow-up receives a new RunId but invokes `exec resume <exact UUID> -` using
the same CLI home and configured workspace. No `--last` or invented session key is
used. A mismatched CLI thread ID fails before publishing false identity. Missing
rollout history cannot be reconstructed by Runiq. The CLI owns persisted history;
Runiq owns no session store. Two active calls for the same confirmed session in one
host are rejected with `CodexSessionBusy`; coordination across processes/hosts and
other CLI clients remains the caller's responsibility.

The trusted host must authorize the supplied session and bind it to the correct
user/workspace/agent. The existing public chat request DTO deliberately does not
accept arbitrary session IDs: continuation is available through `AgentQuery` in
host code. No multi-tenant session ownership or dashboard continuation UI is implied.
Model execution explicitly rejects continuation with `AgentSessionNotSupported`.

## Failure codes

| Situation | Code |
| --- | --- |
| No discoverable installation | `CodexNotInstalled` |
| Explicit executable missing/disappeared | `CodexExecutableNotFound` |
| OS launch denied/failed | `CodexProcessStartFailed` |
| Recognized CLI auth failure | `CodexAuthenticationFailed` |
| Invalid workspace/options or recognized CLI configuration failure | `CodexConfigurationInvalid` |
| Other nonzero exit / failed turn | `CodexProcessFailed` |
| Broken pipe / output I/O | `CodexProcessIoFailed` |
| Deadline exceeded | `CodexTimeout` |
| Invalid JSONL, missing terminal, repeated terminal | `CodexOutputInvalid` |
| Prompt / stdout limit exceeded | `CodexInputLimitExceeded` / `CodexOutputLimitExceeded` |
| Invalid/mismatched, missing or concurrently active session | `CodexSessionInvalid` / `CodexSessionNotFound` / `CodexSessionBusy` |
| Runiq tool/RAG binding or unsupported OS | `CodexCapabilityNotSupported` / `CodexPlatformNotSupported` |

Exec does not document stable typed auth/config exit codes. Classification uses
recognized diagnostic phrases; unfamiliar failures fall back to `CodexProcessFailed`
rather than pretending to know their cause. CLI upgrades require contract validation.

## Validation and official capabilities

`CodexCommandTests`, `CodexExecutorTests` and `CodexProcessTests` cover configuration,
fake-process protocol, real OS pipes/descendants, DI, output limits, cancellation,
timeout, disposal, session identity and continuation without a Codex installation.

`CodexLocalIntegrationTests` is skipped unless `RUNIQ_CODEX_INTEGRATION=1` is set.
Optionally set `RUNIQ_CODEX_EXECUTABLE` to an absolute native path. It uses a temporary
empty workspace, read-only sandbox and the current authenticated CLI account for
two real turns; the test consumes account usage and leaves normal CLI session history.

```powershell
dotnet test tests/Runiq.AI.Agents.Tests --filter FullyQualifiedName~Codex
$env:RUNIQ_CODEX_INTEGRATION = '1'
dotnet test tests/Runiq.AI.Agents.Tests --filter FullyQualifiedName~CodexLocalIntegrationTests
```

Protocol choices were verified against official documentation on 2026-09-24:
[non-interactive JSONL, saved authentication and resume](https://developers.openai.com/codex/noninteractive),
[CLI commands and removal of mcp-server](https://learn.chatgpt.com/docs/developer-commands),
and [App Server](https://developers.openai.com/codex/app-server).
App Server offers finer deltas, thread/turn RPCs and interactive approvals but adds
a bidirectional protocol/lifecycle that this first non-interactive adapter does not
need. Its experimental surfaces are not used. No MCP wrapper or extra API client is
required for exec's persisted session continuation.

## Implementation inventory and observed validation

Files added:

- `src/Runiq.AI.Agents/Configuration/CodexExecutorOptions.cs`
- `src/Runiq.AI.Agents/Hosting/RuniqCodexServiceCollectionExtensions.cs`
- `src/Runiq.AI.Agents/Runtime/Codex/CodexAgentExecutor.cs`
- `src/Runiq.AI.Agents/Runtime/Codex/CodexCommand.cs`
- `src/Runiq.AI.Agents/Runtime/Codex/CodexJsonProtocol.cs`
- `src/Runiq.AI.Agents/Runtime/Codex/CodexOutputReader.cs`
- `src/Runiq.AI.Agents/Runtime/Codex/CodexProcessContainment.cs`
- `src/Runiq.AI.Agents/Runtime/Codex/CodexProcessFactory.cs`
- `src/Runiq.AI.Agents/Runtime/Codex/CodexSessionGate.cs`
- `src/Runiq.AI.Agents/Runtime/Codex/ICodexProcess.cs`
- `tests/Runiq.AI.Agents.Tests/Agents/CodexCommandTests.cs`
- `tests/Runiq.AI.Agents.Tests/Agents/CodexExecutorTests.cs`
- `tests/Runiq.AI.Agents.Tests/Agents/CodexLocalIntegrationTests.cs`
- `tests/Runiq.AI.Agents.Tests/Agents/CodexProcessTests.cs`
- `docs/codex-executor.md`

Files updated:

- `src/Runiq.AI.Agents/Configuration/AgentExecutorConfiguration.cs`
- `src/Runiq.AI.Agents/Domain/Agent.cs`
- `src/Runiq.AI.Agents/Hosting/Agents/AgentChatApiHandler.cs`
- `src/Runiq.AI.Agents/Hosting/Agents/AgentChatResponse.cs`
- `src/Runiq.AI.Agents/Hosting/Agents/AgentChatStreamEvent.cs`
- `src/Runiq.AI.Agents/Hosting/Agents/AgentChatStreamEventMapper.cs`
- `src/Runiq.AI.Agents/Models/AgentExecutionEvent.cs`
- `src/Runiq.AI.Agents/Models/AgentExecutionResult.cs`
- `src/Runiq.AI.Agents/Runtime/AgentExecutionRuntime.cs`
- `src/Runiq.AI.Agents/Runtime/AgentQuery.cs`
- `src/Runiq.AI.Agents/Runtime/AgentRunContext.cs`
- `src/Runiq.AI.Agents/Runtime/ModelAgentExecutor.cs`
- `src/Runiq.AI.Agents/Services/AgentExecutionResultBuilder.cs`
- `src/Runiq.AI.Agents/README.md`
- `docs/agent-definition-acceptance.md`
- `docs/agent-execution-lifecycle.md`
- `docs/agent-executor-acceptance.md`

Observed on Windows, 2026-09-24:

- Release solution build and local pack succeeded. No new compiler warnings;
  existing NU1902, RAG NU5104 and non-packable sample warnings remain.
- Latest Agents suite: 539 passed, 1 opt-in local CLI test skipped.
- Codex subset with local integration enabled: 46 passed, none skipped. Real
  execution/continuation passed with CLI 0.154.0; an earlier run also passed using
  the desktop CLI 0.155.0-alpha.16. Linux process containment was not executed here.
- Other solution packages: Core 59, RAG 702, Workflows 36, CLI 10 and OrderSupport
  11 passed. PostgreSQL: 8 passed, 21 failed because 127.0.0.1:54329 was unavailable,
  with a corresponding fixture cleanup error. The full solution is not reported green.
- Test badge script: all 12 checks passed. `git diff --check` passed.
- Existing OrderSupport, solution and AGENTS.md working-tree changes were preserved;
  no commit, remote update or package publication was performed.

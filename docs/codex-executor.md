# Local Codex executor

The agent-selected adapter implements the existing `IAgentExecutor`. It invokes the user's
installed CLI with `codex exec --json`, rather than making a separate model API
call. `ICliProcessFactory` / `ICliProcess` are internal OS-boundary test seams,
shared with Claude Code, not another agent or coding-harness abstraction. Model and Claude registrations,
resolver duplicate detection, and the runtime lifecycle remain unchanged.

## Agent model configuration

`UseCodex(Action<CodexAgentOptions>)` requires an explicit model. There is no
parameterless overload. Null, empty and whitespace models fail immediately;
all other names are passed to Codex without a model catalog or compatibility table.

```csharp
agent.UseCodex(options =>
{
    options.Model = "gpt-6-sol";
    options.ReasoningEffort = CodexReasoningEffort.High; // Default
    options.ServiceTier = CodexServiceTier.Default; // Default
});
```

The validated settings are copied into immutable `agent.Executor.Codex`.
Reasoning levels are None, Minimal, Low, Medium, High, XHigh, Max and Ultra;
the CLI/provider decides whether the selected model supports the requested level.
Different agents in one host have independent settings. Model, effort and tier
are not host `CodexExecutorOptions` properties.

Every call passes `--model <model>` and `-c model_reasoning_effort="high"`
(or the selected lowercase effort). Fast passes `-c service_tier="fast"`, which
Codex maps to priority. Default sends no tier override: local CLI tier settings
and defaults apply, including any locally configured Fast preference. It does
not force the API's standard service tier. These semantics follow the
[official configuration reference](https://learn.chatgpt.com/docs/config-file/config-reference).
`OPENAI_API_KEY` is not required; local Codex CLI authentication is used.

## Host configuration

```csharp
using Runiq.AI.Agents;
using Runiq.AI.Agents.Configuration;
using Runiq.AI.Agents.Runtime;
using Runiq.AI.Core;

builder.Services.AddRuniqServer(server => server.AddAgent(
    new Agent("reviewer", "Reviewer", "Review the repository and explain findings.").UseCodex(options => options.Model = "gpt-6-sol")));
// Optional host/process overrides; executor registration is automatic.
builder.Services.Configure<CodexExecutorOptions>(options =>
{
    options.WorkingDirectory = Path.GetFullPath("/srv/repository");
    options.Timeout = TimeSpan.FromMinutes(5);
    options.Sandbox = CodexSandboxMode.ReadOnly;
    // Optional absolute native executable path, for example C:\\Tools\\codex.exe.
    // options.ExecutablePath = "/usr/local/bin/codex";
});
```

Resolve `AgentExecutionRuntime` inside a DI scope. `UseCodex(...)` selects the
executor and `AddRuniqServer` automatically registers its runtime infrastructure.
No separate executor registration is required. Multiple Codex agents share one
registration; Codex and Claude may coexist. Only selected CLI kinds are registered.
Custom duplicate executor kinds remain configuration errors. Registration does not
run Codex or authenticate. Manually constructed compatibility runtimes still expose
only the executors supplied by their constructors.

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

## Working directory and process settings

The default workspace is `IHostEnvironment.ContentRootPath`. Without a host
environment, the current working directory is used when options are resolved.
An explicit non-empty `WorkingDirectory` wins, whether configured before or after
`AddRuniqServer`. Use `services.Configure<CodexExecutorOptions>(...)` for optional
executable, timeout, output limits and other process settings. These remain host
settings and do not change agent-level model configuration. No service provider
is built during registration; CLI process creation happens only during execution.

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
Each follow-up receives a new RunId but invokes `exec resume --model <model>
-c model_reasoning_effort="<effort>" [tier override] <exact UUID> -` using
the same CLI home and configured workspace. No `--last` or invented session key is
used. A mismatched CLI thread ID fails before publishing false identity. Missing
rollout history cannot be reconstructed by Runiq. The CLI owns persisted history;
Runiq owns no session store. Every resume explicitly reapplies the current agent's
immutable model and effort (and Fast if selected), overriding a different model/effort
in saved history. A query cannot change these settings. If host code deliberately
resumes another agent's session, the current agent configuration wins; the host must
control that choice. Default tier continues to defer to CLI-local configuration. Two active calls for the same confirmed session in one
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
| Invalid, unsupported, inaccessible or unavailable model | `CodexModelNotAvailable` |
| Unsupported model/reasoning combination | `CodexReasoningEffortNotSupported` |
| Other nonzero exit / failed turn | `CodexProcessFailed` |
| Broken pipe / output I/O | `CodexProcessIoFailed` |
| Deadline exceeded | `CodexTimeout` |
| Invalid JSONL, missing terminal, repeated terminal | `CodexOutputInvalid` |
| Prompt / stdout limit exceeded | `CodexInputLimitExceeded` / `CodexOutputLimitExceeded` |
| Invalid/mismatched, missing or concurrently active session | `CodexSessionInvalid` / `CodexSessionNotFound` / `CodexSessionBusy` |
| Runiq tool/RAG binding or unsupported OS | `CodexCapabilityNotSupported` / `CodexPlatformNotSupported` |

For example, a rejected `abcd` model produces:
`Codex model 'abcd' is not available or is not supported by the current Codex CLI/account.`
`AgentExecutionEvent.ErrorDetails` and `AgentExecutionResult.ErrorDetails` preserve
`RequestedModel`, `ExecutorKind`, `ExitCode` and bounded `DiagnosticDetail` from
stderr/protocol errors. Raw diagnostics are local debugging data, excluded from
JSON serialization and hosted messages. Both JSONL failure records and stderr-only
failures are normalized, including a failed turn with exit code zero.

Exec does not document stable typed auth/config exit codes. Classification uses
recognized diagnostic phrases; unfamiliar failures fall back to `CodexProcessFailed`
rather than pretending to know their cause. CLI upgrades require contract validation.

## Validation and official capabilities

`CodexConfigurationTests`, `CodexCommandTests`, `CodexExecutorTests` and `CliProcessTests` cover configuration,
fake-process protocol, real OS pipes/descendants, DI, output limits, cancellation,
timeout, disposal, session identity and continuation without a Codex installation.

`CodexLocalIntegrationTests` is skipped unless `RUNIQ_CODEX_INTEGRATION=1` is set.
Optionally set `RUNIQ_CODEX_EXECUTABLE` to an absolute native path and
`RUNIQ_CODEX_MODEL` to an available model (default `gpt-6-sol`). Execution/resume uses a temporary
empty workspace, read-only sandbox and the current authenticated CLI account for
two real turns. A separate opt-in test verifies deliberate invalid-model error mapping.
These tests consume account usage and leave normal CLI session history.

```powershell
dotnet test tests/Runiq.AI.Agents.Tests --filter "FullyQualifiedName~Codex|FullyQualifiedName~CliProcess"
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

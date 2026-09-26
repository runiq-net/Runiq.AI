# Local Claude Code executor

`UseClaude(options => options.Model = "sonnet")` selects the existing `IAgentExecutor` contract. Adding that agent
through `AddRuniqServer` automatically registers the local CLI adapter; no separate
executor registration, API client or API key option is needed.

## Agent model configuration

`UseClaude(Action<ClaudeAgentOptions>)` requires an explicit model name or alias.
The parameterless overload has been removed; migrate existing `UseClaude()` calls:

```csharp
agent.UseClaude(options =>
{
    options.Model = "sonnet";
    options.ReasoningEffort = ClaudeReasoningEffort.High;
});
```

Null callbacks, blank models and undefined reasoning values fail at definition time without
selecting an executor. Settings are copied into immutable `agent.Executor.Claude`.
Reasoning defaults to `High`; supported configuration values are `Low`, `Medium`, `High`,
`XHigh` and `Max`. The CLI decides model availability and supported effort levels; Runiq
does not maintain a compatibility catalog. There is no Claude service-tier option.

Every invocation, including resume, sends `--model <model>` and `--effort <lowercase effort>`.
`CLAUDE_CODE_EFFORT_LEVEL` is removed from the child environment so it cannot override the
agent's explicit selection. CLI policy may still limit effective effort. Authentication,
permissions, tool bridging and host process options retain their existing behavior.
Studio metadata exposes the configured model and requested reasoning effort.

The flags follow the official [CLI reference](https://code.claude.com/docs/en/cli-reference)
and [effort configuration](https://code.claude.com/docs/en/model-config#adjust-effort-level).

## Host registration

```csharp
using Runiq.AI.Agents;
using Runiq.AI.Agents.Configuration;
using Runiq.AI.Agents.Runtime;
using Runiq.AI.Core;

builder.Services.AddRuniqServer(server => server.AddAgent(
    new Agent("coder", "Coder", "Inspect this project and explain your findings.").UseClaude(options => options.Model = "sonnet")));
// Optional host/process overrides; executor registration is automatic.
builder.Services.Configure<ClaudeExecutorOptions>(options =>
{
    options.WorkingDirectory = Path.GetFullPath("/srv/repository");
    options.Timeout = TimeSpan.FromMinutes(5);
    // Optional absolute native path; Windows requires claude.exe, not a shell shim.
    // options.ExecutablePath = @"C:\Tools\claude.exe";
});
```

Resolve `AgentExecutionRuntime` from a DI scope. Registration is idempotent and
coexists with Model and Codex. Custom duplicate executor kinds still fail resolution.
Only executor kinds selected by registered agents are enabled. Registration
does not launch or authenticate. One typed options registration configures one
workspace for all Claude agents in that host. Configuration belongs to the trusted
host, not chat users. Direct agent execution retains its existing unsupported contract.

## Working directory and process settings

The default workspace is `IHostEnvironment.ContentRootPath`. Without a host
environment, the current working directory is used when options are resolved.
An explicit non-empty `WorkingDirectory` wins, whether configured before or after
`AddRuniqServer`. Use `services.Configure<ClaudeExecutorOptions>(...)` for optional
executable, timeout, output limits and other process settings. These remain host
settings and do not change agent-level model configuration. No service provider
is built during registration; CLI process creation happens only during execution.

## CLI, authentication and permissions

The adapter launches `claude --print --output-format stream-json --verbose
--include-partial-messages --permission-mode dontAsk --model <model> --effort <effort>`. Agent instructions precede
the request in stdin; neither is interpreted by a shell or placed in arguments.
CLI/project instructions retain Claude's normal precedence. The child inherits
the current account's home and Claude configuration, including its authentication,
with agent model/effort overrides and the effort environment exclusion described above. Runiq does not read credentials,
log in, inject keys or make independent model API calls.

`dontAsk` denies actions requiring interactive approval; existing Claude permission
allow rules still apply. This is not Codex's read-only sandbox and does not guarantee
a read-only filesystem. Configure Claude permissions under the trusted host account
and project. Runiq never enables permission bypass. Claude's tools, hooks and MCP
remain CLI-owned. Agent-bound Runiq tools use the automatic MCP bridge described
below. Active RAG is still rejected with `ClaudeCapabilityNotSupported`.

Discovery considers absolute PATH entries only. Windows supports native `claude.exe`
and the npm `node_modules/@anthropic-ai/claude-code/bin/claude.exe` layout. `.cmd`
and `.ps1` wrappers are not executed. Other layouts require a supported executable
path or an absolute PATH entry. The workspace must be an existing absolute directory.

## Streaming and continuation

Batch and streaming use the same path. Main-conversation `text_delta` events become
`AssistantDelta` immediately. Claude emits complete assistant records per content
block, sharing `message.id`; duplicate detection therefore uses record UUIDs.
Complete blocks provide a fallback when partial text is absent and are not appended
again after streaming. The terminal result is also not duplicated. Thinking, native
tool activity, subagent text and additive telemetry are not exposed as main assistant
text or Runiq tool events. Success requires a successful `result`, EOF and zero exit.
Malformed/incomplete protocol, repeated terminal records or changed sessions fail.
Runtime owns event sequence, timestamps, aggregation and terminal publication.

```csharp
var first = await runtime.ExecuteAsync("coder", "Explain the entry point.", cancellationToken);
if (first.IsSuccess && first.ProviderSessionId is { } session)
{
    var next = await runtime.ExecuteAsync("coder",
        new AgentQuery("Explain its dependencies.") { ProviderSessionId = session },
        cancellationToken);
}
```

A CLI-confirmed UUID from `system/init` or `result` supplies `ProviderSessionId`.
Passing it invokes `--resume <exact UUID>`, preserving Claude's persisted history.
A fresh invocation has no resume flag. Every invocation receives a fresh runtime
`RunId`; it is never used as a session key. `--continue` selects the most recent
session in the directory and is deliberately not used for concurrent host callers.
Session names, latest-session selection and forking are not exposed. Missing history
is an error, never a fabricated new conversation.

The same session cannot run concurrently within one host; the gate is released on
completion, failure or disposal. Other processes/CLI users are not coordinated.
The host must authorize session ownership and bind it to the correct workspace and
agent. Continuation is available through `AgentQuery`; no dashboard continuation
UI or new HTTP request session field is added.

## Lifecycle and failure codes

Shared `Runtime/Cli` infrastructure retains the Codex native launch and containment
algorithms. Windows launches with Job Object membership already attached and
kill-on-close semantics. Linux uses a `setsid` process group (util-linux required).
Cancellation, disposal and parent exit terminate owned descendants. Cleanup waits
up to ten seconds and uses the runtime cleanup contract. Only Windows and Linux
are supported. Deliberate Linux process-group escape is outside this ownership
guarantee; containment is not a security sandbox.

Stdout has configurable record/total limits (1 MiB/8 MiB characters by default).
The prompt also uses the record limit. Stderr is drained concurrently, retaining
at most 16 KiB for private classification. Timeout includes output consumption and
defaults to ten minutes. A paused consumer's process still receives cancellation;
dispose unfinished enumerators promptly. Very slow consumption can encounter the
CLI's own output-drain deadline. Raw diagnostics and prompts are not logged/returned.

| Condition | Outcome |
| --- | --- |
| No discoverable installation | `ClaudeNotInstalled` |
| Configured executable missing | `ClaudeExecutableNotFound` |
| OS launch failure | `ClaudeProcessStartFailed` |
| Recognized auth/configuration diagnostic | `ClaudeAuthenticationFailed` / `ClaudeConfigurationInvalid` |
| Other nonzero exit or error result | `ClaudeProcessFailed` |
| Deadline exceeded | `ClaudeTimeout` |
| Caller cancellation | `AgentRunCanceledException`, runtime status `Cancelled` |
| Invalid/incomplete protocol | `ClaudeOutputInvalid` |
| Input/output limit | `ClaudeInputLimitExceeded` / `ClaudeOutputLimitExceeded` |
| Invalid, missing or busy session | `ClaudeSessionInvalid` / `ClaudeSessionNotFound` / `ClaudeSessionBusy` |
| Pipe failure | `ClaudeProcessIoFailed` |
| Unsupported OS | `ClaudePlatformNotSupported` |

CLI diagnostic classification is heuristic, not a stable typed error API. Unknown
diagnostics remain generic failures. Timeout is a failed run; caller cancellation
retains the existing exception contract.

## Validation

Normal tests require neither Claude nor authentication. Tests cover process start,
streaming and block deduplication, aggregation, sessions/resume, errors, limits,
cancellation, timeout, disposal and DI. Shared real-process tests cover Windows
startup/quoting, pipes, child cleanup and Job membership before wrapper attachment.

```powershell
dotnet test tests/Runiq.AI.Agents.Tests --filter "FullyQualifiedName~Claude|FullyQualifiedName~Codex|FullyQualifiedName~CliProcess"
$env:RUNIQ_CLAUDE_INTEGRATION = '1'
# Optional: $env:RUNIQ_CLAUDE_EXECUTABLE = 'C:\Tools\claude.exe'
dotnet test tests/Runiq.AI.Agents.Tests --filter FullyQualifiedName~ClaudeLocalIntegrationTests
```

The opt-in test uses a temporary workspace and the current CLI account for two real
turns and checks recall through resume. It consumes account usage and leaves normal
CLI session history. Observed on Windows, 2026-09-24:

- Release solution build succeeded, with seven existing NU1902 package warnings.
- Final Release Agents suite: 600 passed, 2 opt-in tests skipped, 0 failed.
- Codex regressions and real Windows process/child lifecycle tests passed.
- Local Claude Code 2.1.199 was discovered and started through the adapter.
  `claude auth status` reported no login. The opt-in test failed with the correctly
  normalized `ClaudeAuthenticationFailed`; successful live generation/resume and
  Linux execution were not validated.
- `git diff --check` passed. No commit or remote modification was made.

Protocol choices follow the official [CLI reference](https://code.claude.com/docs/en/cli-reference),
[programmatic execution](https://code.claude.com/docs/en/headless) and
[per-block streaming order](https://code.claude.com/docs/en/agent-sdk/streaming-output),
checked on 2026-09-24. Unknown telemetry is ignored; required text/session/result
shapes are validated.

## Change inventory

Added:

- `src/Runiq.AI.Agents/Configuration/ClaudeExecutorOptions.cs`
- `src/Runiq.AI.Agents/Hosting/RuniqClaudeServiceCollectionExtensions.cs`
- `src/Runiq.AI.Agents/Runtime/Claude/ClaudeAgentExecutor.cs`
- `src/Runiq.AI.Agents/Runtime/Claude/ClaudeCommand.cs`
- `src/Runiq.AI.Agents/Runtime/Claude/ClaudeJsonProtocol.cs`
- `src/Runiq.AI.Agents/Runtime/Claude/ClaudeSessionGate.cs`
- `src/Runiq.AI.Agents/Runtime/Cli/CliProcessException.cs`
- `tests/Runiq.AI.Agents.Tests/Agents/ClaudeCommandTests.cs`
- `tests/Runiq.AI.Agents.Tests/Agents/ClaudeExecutorTests.cs`
- `tests/Runiq.AI.Agents.Tests/Agents/ClaudeProtocolTests.cs`
- `tests/Runiq.AI.Agents.Tests/Agents/ClaudeLocalIntegrationTests.cs`
- `docs/claude-executor.md`

Moved/generalized from `Runtime/Codex` to `Runtime/Cli` (all `.cs`):
`ICodexProcess` to `ICliProcess`, `CodexProcessFactory` to `CliProcessFactory`,
`CodexProcessContainment` to `CliProcessContainment`, `CodexWindowsProcess` to
`CliWindowsProcess`, and `CodexOutputReader` to `CliOutputReader`.
`CodexProcessTests.cs` moved to `CliProcessTests.cs` in the existing test directory.
Native lifecycle algorithms are unchanged; shared errors receive their original
provider-specific prefix at the executor boundary.

Updated:

- `src/Runiq.AI.Agents/Configuration/AgentExecutorConfiguration.cs`
- `src/Runiq.AI.Agents/Domain/Agent.cs`
- `src/Runiq.AI.Agents/Hosting/RuniqCodexServiceCollectionExtensions.cs`
- `src/Runiq.AI.Agents/Runtime/Codex/CodexAgentExecutor.cs`
- `tests/Runiq.AI.Agents.Tests/Agents/CodexExecutorTests.cs`
- `src/Runiq.AI.Agents/README.md`
- `docs/codex-executor.md`
- `docs/agent-execution-lifecycle.md`

## Runiq tools and dashboard events

Attach existing typed Runiq tools directly to a Claude agent:

```csharp
new Agent("assistant", "Assistant", "Use change_summary for supplied change counts.")
    .UseClaude(options => options.Model = "sonnet")
    .AddTool<ChangeSummaryTool>();
```

`ChangeSummaryTool` is an example `IRuniqTool<TInput,TOutput>` implementation.
`AddRuniqServer` handles executor registration; no additional MCP registration or
user-managed server is required. Agents without tools start no bridge.

A run-owned, authenticated loopback HTTP MCP endpoint exposes only that agent's
bindings. The transport is shared with Codex and reuses `AgentToolInvoker`, input
schemas, scoped dependencies, output limits, cancellation and cleanup. Calls within
one run are serialized to protect scoped dependencies. Separate runs have separate
credentials and endpoints. Tool code runs inside the host process with host
permissions, and must honor cancellation.

Claude receives an inline `--mcp-config` JSON object with a reserved
`runiq_agent_tools` server, `type: http`, and an Authorization header referencing
`${RUNIQ_CLI_TOOL_TOKEN}`. The random token exists only in the child environment,
not command arguments or a persisted configuration file. `--allowedTools` lists
only the agent's exact `mcp__runiq_agent_tools__<tool-name>` names. The existing
`dontAsk` mode remains enabled; permission bypass is never used. Host-bound tool
names must contain only ASCII letters, digits, underscores, dots or hyphens so
that they cannot introduce permission wildcards. Existing CLI MCP configuration
is preserved; the reserved bridge server is refreshed on every run and resume.

These settings follow the official [MCP configuration](https://code.claude.com/docs/en/mcp)
and [CLI reference](https://code.claude.com/docs/en/cli-reference). The local CLI
version inspected was 2.1.199. A tool-enabled run requires the init event to report
`runiq_agent_tools` as connected; missing, pending or failed connections return
`ClaudeToolBridgeFailed` instead of silently continuing without registered tools.

The bridge publishes `ToolCallStarted`, `ToolCallCompleted` and `ToolCallFailed`
through the existing runtime/SSE contract. Dashboard tool cards display the tool
name, input, result or safe failure. Native Claude MCP/tool telemetry is not
republished as Runiq tool events, avoiding duplicate cards. Tool failures are also
returned as MCP `isError` results so Claude can explain or recover from them.

Example dashboard prompt for an agent bound to the Codex sample's change-summary tool:

```text
Use change_summary to summarize a.cs +45/-12 and b.cs +18/-4.
Report the totals. Do not modify files.
```

Expected output: two files, 63 added lines and 16 deleted lines, with a completed
tool card. A correct text answer alone does not prove tool execution.

Deterministic tests use a fake Claude process with real local HTTP MCP requests
and cover invocation, dashboard events, errors, scope isolation, resume, limits,
cancellation, timeout and abandoned-stream cleanup. A separate
`LocalCli_InvokesRuniqToolAndResumes` test is enabled with
`RUNIQ_CLAUDE_INTEGRATION=1` and requires an authenticated installed CLI.

# Runiq Codex Repository Assistant

This .NET 10 web sample uses Runiq's embedded dashboard to chat with one read-only
repository agent backed by the local Codex CLI. It follows the Expense sample's
structure: like OrderSupport, it uses `Microsoft.NET.Sdk.Web`. The agent is defined
in `Agents/CodexAgent.cs`, while
`Program.cs` contains only host, executor and dashboard registration.

## Run

Install the .NET 10 SDK and Codex CLI, and sign in to Codex under the same account
that runs this sample. Windows and Linux are supported; Linux requires `setsid`.
Runiq makes no direct OpenAI model API calls here. `OPENAI_API_KEY` is not required.

From the repository root:

```sh
dotnet run --project samples/Runiq.AI.CodexAgent --launch-profile http
```

Open [http://localhost:5298/dashboard](http://localhost:5298/dashboard), select
**CodexAgent**, and send:

Try different requests:

- `hi` - a greeting and short help, without automatic repository inspection.
- `Explain Program.cs and why UseCodex(options => options.Model = "gpt-6-sol") does not require an API key. Cite relevant lines.`
- `Review Agents/CodexAgent.cs for ambiguous instructions and suggest a clearer version without changing files.`
- `Find the bug in this C# method and propose a fix: int Max(int a, int b) => a < b ? a : b;`

The user input determines the task. The agent can explain specific files, review
pasted code, or propose changes as code/diff in its answer. Repository-specific
answers cite file paths and lines. It stays read-only: suggestions are not applied.
Each request is self-contained; include the code/file and question rather than
relying on earlier dashboard messages. There is no separate frontend, custom chat
endpoint, manual event loop or console execution workflow.

## Agent and dashboard registration

1. `AddRuniqServer` registers `CodexAgent.Create()`. `UseCodex(...)` selects the
   executor; Runiq automatically registers the required Codex runtime infrastructure.
2. `UseRuniqDashboard` serves Runiq's embedded UI at `/dashboard`.

No separate executor registration is needed. `WorkingDirectory` defaults to the
host's `ContentRootPath`, which is this sample's project directory when launched
as shown. `OPENAI_API_KEY` is not required; local Codex CLI authentication is used.
For optional process limits or a different workspace, use standard
`builder.Services.Configure<CodexExecutorOptions>(...)` with the
`Runiq.AI.Agents.Configuration` namespace. This configures settings only; automatic
registration still follows the registered agent's executor selection.

No sample-specific configuration or infrastructure is needed. The executor's
existing defaults provide a read-only sandbox, a ten-minute timeout, and native
Codex discovery on PATH (including the standard Windows npm layout). On Windows,
use the native `codex.exe`, not a shell shim. The project must remain inside a Git
repository. For advanced host options, see the [Codex executor documentation](../../docs/codex-executor.md).

The agent prohibits file modifications and destructive commands. .NET build
artifacts and Codex's own local session storage are separate from inspected sources.

CLI installation, authentication and other failures use the existing Runiq
dashboard error handling and executor error codes. Missing Codex and missing
authentication remain distinct; no sample-specific error translation is added.
Requests do not crash the host or fall back to a model API client.

Each request creates a new runtime run and Codex conversation using the existing
CLI authentication. The confirmed `ProviderSessionId` is retained in the existing
HTTP/SSE response metadata; this sample adds no dashboard session/resume controls.

As in Expense, the dashboard allows anonymous access for the local sample. The
launch profile binds to localhost; configure authentication before exposing it
to other users or networks. Inspection remains subject to Codex permissions and
account availability. No sandbox bypass is enabled.

## Model selection

The Codex model is explicitly selected at the Runiq Agent level. A Codex agent
cannot be configured without a non-null, non-empty, non-whitespace model:

```csharp
.UseCodex(options =>
{
    options.Model = "gpt-6-sol";
    // Optional overrides (these are the defaults):
    options.ReasoningEffort = CodexReasoningEffort.High;
    options.ServiceTier = CodexServiceTier.Default;
});
```

The enums are in `Runiq.AI.Agents.Configuration`. ReasoningEffort defaults to High;
ServiceTier defaults to Default. Default omits a tier override and preserves the
local CLI's configured/default tier; it does not force standard service if the CLI
is configured for Fast. Fast sends `-c service_tier="fast"` (Codex maps this to priority).
Model and effort are explicitly sent on both new and resumed turns. The immutable
agent configuration takes precedence over the saved session model/effort; there is
no per-query model override. Different agents can use different settings in one host.

Runiq has no model-name whitelist or model/effort compatibility table. If, for example,
`Model = "abcd"` is rejected by Codex CLI, Runiq normalizes the runtime failure to
`CodexModelNotAvailable` with a message identifying the requested model. Unsupported
reasoning combinations map to `CodexReasoningEffortNotSupported`. Execution events/results
retain model, executor, exit code and local diagnostic detail in `ErrorDetails`.
Raw diagnostic detail is excluded from JSON and user-facing messages.

`OPENAI_API_KEY` is not required: execution uses local Codex CLI authentication.
Host `CodexExecutorOptions` only controls executable, workspace, timeout, sandbox,
Git checks and output limits. It does not contain model, effort or tier settings.
See the [Codex executor documentation](../../docs/codex-executor.md) for continuation,
error handling and opt-in integration tests, and the
[official configuration reference](https://learn.chatgpt.com/docs/config-file/config-reference).

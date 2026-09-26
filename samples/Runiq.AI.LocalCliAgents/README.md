# Runiq Local CLI Agents

Chat with Codex and Claude agents in the Runiq dashboard: summarize file changes with a
deterministic C# tool, or review code and get suggested fixes and tests.

| Agent | Purpose | Model | Reasoning | Service tier |
| --- | --- | --- | --- | --- |
| QuickProjectAssistant | Summarizes supplied change counts using `change_summary` | `gpt-5.6-sol` | Medium | Fast |
| CodeReviewer | Reviews code and suggests fixes and test cases | `gpt-6-astra` | High | Default |
| ClaudeProjectAssistant | Summarizes supplied change counts using `change_summary` | `sonnet` | High | Not configured by Runiq |

## Run

Requirements: .NET 10 SDK and the CLI for the agents you want to use: Codex or
Claude Code with HTTP MCP support, installed and signed in under the account
running the sample. Linux also requires `setsid`. Agents use local CLI authentication;
the sample does not require `OPENAI_API_KEY`.

You can start the dashboard without either CLI installed. Installation and
authentication errors appear when you send a request to the corresponding agent.

From the repository root:

```sh
dotnet run --project samples/Runiq.AI.LocalCliAgents --launch-profile http
```

Open [http://localhost:5298/dashboard](http://localhost:5298/dashboard) and select
an agent from the Agents page.

## Try QuickProjectAssistant

Send this prompt:

```text
Use the change_summary tool to summarize these changes:
- OrderService.cs: +45 / -12
- OrderController.cs: +18 / -4
- OrderServiceTests.cs: +90 / -0

Explain the result in three short bullet points. Do not modify files.
```

The chat displays a **Change Summary** tool call with this result, followed by
an explanation:

```json
{"files":3,"added":153,"deleted":16}
```

The tool calculates totals from the supplied numbers without accessing files or
external services. Expand the tool card to inspect its input and output.

## Try CodeReviewer

Send this prompt:

```text
Review this C# method:

decimal CalculateTotal(decimal price, int quantity)
    => price + quantity;

It should calculate the total cost from the unit price and quantity.
Explain the bug, suggest corrected code, and provide three test cases.
Do not modify files.
```

Expect a recommendation to multiply price by quantity, a corrected method, and
test cases. CodeReviewer has no Runiq tools attached.

## Try ClaudeProjectAssistant

Select **ClaudeProjectAssistant** and send:

```text
Use change_summary to summarize a.cs +45/-12 and b.cs +18/-4.
Report the totals in three short bullet points. Do not modify files.
```

With Claude Code installed and authenticated, expect a **Change Summary** tool card
and totals of **2 files, 63 added lines, and 16 deleted lines**.
If the CLI is not installed or cannot be found on PATH, the dashboard shows a
Claude installation error. There is no fallback to Codex or another provider.
After installing and signing in to Claude Code, restart the sample and retry.
Authentication failures are reported separately from missing installation.

## Configure your agents

Agent definitions live in `Agents/`. Select a model explicitly with `UseCodex`
and attach typed tools with `AddTool<T>`:

```csharp
.UseCodex(options =>
{
    options.Model = "gpt-5.6-sol";
    options.ReasoningEffort = CodexReasoningEffort.Medium;
    options.ServiceTier = CodexServiceTier.Fast;
})
.AddTool<ChangeSummaryTool>();
```

`AddRuniqServer` registers the required executor and tool connection automatically.
For Codex, model is required; reasoning defaults to **High** and service tier to **Default**.
Default preserves local CLI tier settings. Model and Fast availability depend on
your CLI/account; model rejections become meaningful Runiq runtime errors.

The Claude agent selects its model and reasoning effort explicitly:

```csharp
.UseClaude(options =>
{
    options.Model = "sonnet";
    options.ReasoningEffort = ClaudeReasoningEffort.High;
})
.AddTool<ChangeSummaryTool>();
```

Claude requires a model and defaults to High reasoning. It uses existing CLI authentication;
there is no parameterless `UseClaude()` overload or Claude service-tier option. The selected
model and effort are also passed on resumed turns and displayed in Studio metadata.

Include all relevant context in each prompt; this sample does not carry dashboard
chat history between requests. The dashboard allows anonymous access for local use.

See the [Codex executor guide](../../docs/codex-executor.md) and
[Claude executor guide](../../docs/claude-executor.md) for process settings,
tool execution boundaries, session continuation, and troubleshooting.

# Runiq Codex Project Assistants

Chat with two Codex agents in the Runiq dashboard: summarize file changes with a
deterministic C# tool, or review code and get suggested fixes and tests.

| Agent | Purpose | Model | Reasoning | Service tier |
| --- | --- | --- | --- | --- |
| QuickProjectAssistant | Summarizes supplied change counts using `change_summary` | `gpt-5.6-sol` | Medium | Fast |
| CodeReviewer | Reviews code and suggests fixes and test cases | `gpt-6-astra` | High | Default |

## Run

Requirements: .NET 10 SDK and a Codex CLI with Streamable HTTP MCP support,
installed and signed in under the account running the sample. Linux also requires
`setsid`. Both agents use **local Codex CLI authentication**; `OPENAI_API_KEY` is
not required.

From the repository root:

```sh
dotnet run --project samples/Runiq.AI.CodexAgent --launch-profile http
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
Model is required; reasoning defaults to **High** and service tier to **Default**.
Default preserves local CLI tier settings. Model and Fast availability depend on
your CLI/account; model rejections become meaningful Runiq runtime errors.

Include all relevant context in each prompt; this sample does not carry dashboard
chat history between requests. The dashboard allows anonymous access for local use.

See the [Codex executor guide](../../docs/codex-executor.md) for process settings,
tool execution boundaries, session continuation, and troubleshooting.

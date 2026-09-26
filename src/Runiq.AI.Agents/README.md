# Runiq.AI.Agents

![NuGet Version](https://img.shields.io/nuget/v/Runiq.AI.Agents?label=nuget)

Code-first AI agents for .NET.

`Runiq.AI.Agents` provides the core agent model for Runiq AI. Use it to define agents in C#, attach strongly typed tools, configure model providers, and build agent-based applications with structured execution support.

## Why Runiq.AI.Agents?

Runiq.AI.Agents is designed for .NET developers who want to build AI agents without leaving the C# ecosystem.

It focuses on:

- Code-first agent definitions
- Strongly typed tool execution
- Provider-aware model configuration
- Runtime-friendly agent composition
- Streaming and structured execution support
- Integration with the broader Runiq AI platform

## Install

```powershell
dotnet add package Runiq.AI.Agents --version 1.0.0
```

## Create an Agent

```csharp
using Runiq.AI.Agents;

var agent = new Agent(
    id: "weather-agent",
    name: "Weather Agent",
    instructions: "Answer weather questions using the available tools.",
    model: "openai/gpt-5",
    apiKey: configuration["OpenAI:ApiKey"]);
```

This constructor selects model-provider execution. Its arguments are:

- `id`: stable identifier used by the runtime
- `name`: human-readable agent name
- `instructions`: system-level behavior definition
- `model`: target model identifier
- `apiKey`: provider credential

Alternatively, create an agent with `id`, `name` and `instructions`, then select exactly
one executor with `UseModel(...)`, `UseCodex(...)` or `UseClaude(options => options.Model = "sonnet")`. Complete the selection
before registration. An agent cannot switch executors after a successful selection.

### Codex CLI

`UseCodex` runs the locally installed Codex CLI. Set an explicit CLI model name
(without the `openai/` provider prefix used by `UseModel`):

```csharp
using Runiq.AI.Agents;
using Runiq.AI.Agents.Configuration;

var codexAgent = new Agent(
        id: "codex-reviewer",
        name: "Codex Reviewer",
        instructions: "Review the supplied code and suggest improvements.")
    .UseCodex(options =>
    {
        options.Model = "gpt-6-astra";
        options.ReasoningEffort = CodexReasoningEffort.High;
        options.ServiceTier = CodexServiceTier.Default;
    });
```

`Model` is required; there is no parameterless `UseCodex()` overload. `ReasoningEffort`
defaults to `High`, and `ServiceTier` defaults to `Default`, which preserves local CLI
tier settings. `Fast` requests the CLI fast tier. Choose a model and reasoning level
supported by your CLI/account; Runiq does not maintain a model availability catalog.
The validated settings are available through `codexAgent.Executor!.Codex`.

Execution uses the host account's existing Codex CLI authentication; no agent API key
is needed. The adapter removes `OPENAI_API_KEY` and `CODEX_API_KEY` from the child
environment. The default CLI sandbox is read-only, and the working directory must be
a Git repository unless the host explicitly sets `SkipGitRepositoryCheck`.
See the [Codex executor guide](../../docs/codex-executor.md) for configuration and continuation.

### Claude CLI

`UseClaude(...)` runs the locally installed Claude Code CLI with an explicit model and
reasoning effort. It uses existing CLI authentication and needs no agent API key:

```csharp
using Runiq.AI.Agents;

var claudeAgent = new Agent(
        id: "claude-analyst",
        name: "Claude Analyst",
        instructions: "Explain the supplied code and suggest test cases.")
    .UseClaude(options =>
    {
        options.Model = "sonnet";
        options.ReasoningEffort = Runiq.AI.Agents.Configuration.ClaudeReasoningEffort.High;
    });
```

The callback and a non-blank `Model` are required; the former parameterless `UseClaude()`
has been removed. Migrate existing calls to `UseClaude(options => options.Model = "sonnet")`.
`ReasoningEffort` defaults to `High` and accepts `Low`, `Medium`, `High`, `XHigh`, or `Max`.
Settings are copied into `agent.Executor!.Claude` and sent on every turn, including resume.
Model names/aliases and effort support are validated by the CLI. Claude has no Runiq
`ServiceTier` option. See the [CLI reference](https://code.claude.com/docs/en/cli-reference).

The adapter uses non-interactive `dontAsk` permission mode: actions that require approval
are denied, while existing CLI allow rules still apply. This mode does not guarantee a
read-only filesystem. See the [Claude executor guide](../../docs/claude-executor.md)
for permission configuration and continuation.

### Register and run CLI agents

Register either or both definitions in an ASP.NET Core host. `AddRuniqServer` automatically
registers the selected CLI executors and their tool infrastructure:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Runiq.AI.Agents.Configuration;
using Runiq.AI.Core;

// Use the codexAgent and claudeAgent definitions above.
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRuniqServer(options =>
{
    options.AddAgent(codexAgent);
    options.AddAgent(claudeAgent);
});

// Optional host-wide process settings for each CLI kind.
builder.Services.Configure<CodexExecutorOptions>(options =>
    options.Timeout = TimeSpan.FromMinutes(5));
builder.Services.Configure<ClaudeExecutorOptions>(options =>
    options.Timeout = TimeSpan.FromMinutes(5));

var app = builder.Build();
app.UseRuniqDashboard(options =>
{
    options.Path = "/dashboard";
    options.Authentication(auth => auth.AllowAnonymous()); // Local demo only.
});
app.Run();
```

Open `/dashboard` to send a request, or resolve `AgentExecutionRuntime` inside a DI scope
for programmatic execution. Calling `Agent.ExecuteAsync` directly is unsupported.
Definition and registration do not launch a CLI; execution requires the corresponding
CLI installed and authenticated under the host account. The adapters support Windows
and Linux; Linux also requires `setsid`. On Windows, use native executables, not `.cmd`
or PowerShell shims.

`WorkingDirectory` defaults to the host content root (the current directory without a
host environment). Override it with an existing absolute path through the relevant
executor options. `ExecutablePath` and `Timeout` are also host settings; the default
timeout is ten minutes. These settings apply to all agents using that CLI kind in the host.

Both CLI agents support `.AddTool<T>()` through an automatic per-run MCP bridge and show
Runiq tool events in the dashboard. Tool code executes in the host process with host
permissions, outside the CLI sandbox. Active Runiq RAG bindings are unsupported and fail
with `CodexCapabilityNotSupported` or `ClaudeCapabilityNotSupported`; use model execution
for the RAG configuration below. CLI failures do not fall back to a model provider.

For a runnable example with typed tools, see [Local CLI Agents](../../samples/Runiq.AI.LocalCliAgents/README.md).

## Add a Tool

Tools allow agents to call strongly typed C# code.

```csharp
using Runiq.AI.Agents.Tools;

[RuniqTool("get_weather", "Gets the current weather for a city.")]
public sealed class WeatherTool : IRuniqTool<WeatherInput, WeatherOutput>
{
    public Task<WeatherOutput> ExecuteAsync(
        WeatherInput input,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            new WeatherOutput(input.City, "Clear"));
    }
}

public sealed record WeatherInput(string City);

public sealed record WeatherOutput(string City, string Condition);
```

Attach the tool to the agent:

```csharp
var agent = new Agent(
        id: "weather-agent",
        name: "Weather Agent",
        instructions: "Use tools when weather data is requested.",
        model: "openai/gpt-5",
        apiKey: configuration["OpenAI:ApiKey"])
    .AddTool<WeatherTool>();
```

## RAG Execution and Grounding Policies

Configure framework-owned retrieval and grounding through `UseRag`:

```csharp
using Runiq.AI.Agents.Configuration;

var agent = new Agent(
        id: "policy-assistant",
        name: "Policy Assistant",
        instructions: "Answer employee policy questions.",
        model: "openai/gpt-5",
        apiKey: configuration["OpenAI:ApiKey"])
    .UseRag(rag =>
    {
        rag.IndexName = "company-policies";
        rag.Mode = RagExecutionMode.Required;
        rag.NoContextBehavior = RagNoContextBehavior.ReturnNotFound;
        rag.Acceptance.MinimumRelevance = 0.75;
        rag.Acceptance.CandidateCount = 20;
        rag.Acceptance.MaximumAcceptedResults = 5;
        rag.Reranking.Enabled = true;
        rag.Reranking.MaximumCandidates = 5;
        rag.Reranking.Timeout = TimeSpan.FromSeconds(5);
        rag.Reranking.FailurePolicy = RagRerankerFailurePolicy.UseOriginalOrder;
        rag.ContextBudget.MaximumContextTokens = 32_768;
        rag.ContextBudget.ResponseTokenReserve = 4_096;
        rag.ContextBudget.MaximumChunksPerSource = 2;
        rag.ContextBudget.PreferSourceDiversity = true;
    });
```

`Open` is the default execution mode. It uses accepted document context when available and, with the default
`AnswerNormally` behavior, preserves normal agent execution when retrieval returns no accepted context.
`Grounded` treats accepted documents as the primary source, requires unsupported information to be separated,
forbids invented company policies, and requires conflicting sources to be identified. `Required` constrains the
answer to accepted context and therefore cannot be combined with `AnswerNormally`; that combination fails during
configuration and is validated again before retrieval at runtime.

No-context behavior is selected independently for every valid combination:

| Behavior | Outcome when no context is accepted |
|---|---|
| `AnswerNormally` | Invokes the model without accepted context. Invalid with `Required`. |
| `ReturnNotFound` | Returns a successful framework-owned not-found response and skips the model. |
| `FailExecution` | Returns `RagContextUnavailable` as an execution failure and skips the model. |

`CandidateCount` (default 20) is the vector-search candidate budget, not an accepted-result guarantee.
`MaximumAcceptedResults` (default 5) limits context after every candidate has been evaluated; otherwise acceptable
results outside the limit remain visible as `ResultLimitExceeded` rejections. `MinimumRelevance` is an optional
threshold in the inclusive provider-independent `[0,1]` range. The default null threshold does not manufacture
relevance for an unsupported metric.

Reranking is disabled by default. When enabled, the runtime sends at most `MaximumCandidates` accepted results to
the registered provider-neutral `IRagReranker` after acceptance and before token-budget selection. A rerank score
is always a separate `[0,1]` higher-is-better value; it never replaces semantic, lexical, RRF, raw, or normalized
retrieval scores. Equal scores use original accepted rank and then ordinal document/chunk identity. The reranker
must return every requested identity exactly once; unknown, duplicate, missing, non-finite, or out-of-range output
invalidates the complete response.

`FailurePolicy = Fail` prevents model execution. `UseOriginalOrder` preserves the exact accepted retrieval order
for unavailable services, timeouts, exceptions, and invalid output, and exposes the fallback through structured
metadata. Caller cancellation still propagates, while `Timeout` is classified separately.

### Answerability acceptance criteria

Answerability is an agent execution policy, not another retrieval acceptance score. The following rules are
normative:

1. Only a successful reranking result with aggregate `Answerable` establishes answerability for `Grounded` and
   `Required`. Aggregate `Unknown` and `NotAnswerable` both fail closed: all reranked candidates are excluded from
   model context, `NoContextReason` is `NotAnswerable`, and the configured `NoContextBehavior` is applied. The
   reranking metadata retains the original aggregate value, so operators can distinguish `Unknown` from
   `NotAnswerable` even though their execution outcome is the same.
2. Candidate-level answerability is observability metadata only. It does not independently include or exclude a
   candidate, override aggregate answerability, or change context assembly.
3. Aggregate answerability is authoritative when aggregate and candidate signals disagree. For example, one or
   more `Answerable` candidates with aggregate `NotAnswerable` still produce no context in `Grounded` and
   `Required`; aggregate `Answerable` does not remove an individual candidate marked `NotAnswerable`.
4. `Open` deliberately ignores answerability for execution gating. It keeps the reranked evidence, applies the
   reranked order, and invokes the model normally while still publishing aggregate and candidate answerability for
   observability.
5. Answerability gating applies only when the reranker outcome is `Succeeded`. `Fallback` follows
   `FailurePolicy = UseOriginalOrder` and preserves the pre-rerank accepted context; `Failed` with
   `FailurePolicy = Fail` blocks model execution as a reranking failure rather than a no-context outcome.

The resulting successful-rerank behavior is:

| RAG mode | Aggregate `Answerable` | Aggregate `Unknown` | Aggregate `NotAnswerable` |
|---|---|---|---|
| `Open` | Use reranked context | Use reranked context | Use reranked context |
| `Grounded` | Use reranked context | Apply no-context policy | Apply no-context policy |
| `Required` | Use reranked context | Apply no-context policy | Apply no-context policy |

### Cohere production reranker

`Runiq.AI.Agents` includes a supported cross-encoder integration for Cohere Rerank v2. Register it before
`AddRuniqServer`; keep the credential in a secret provider or environment variable rather than source-controlled
configuration:

```csharp
using Runiq.AI.Agents.Providers.Cohere;

builder.Services.AddCohereReranker(options =>
{
    options.ApiKey = builder.Configuration["COHERE_API_KEY"]
        ?? throw new InvalidOperationException("COHERE_API_KEY is required.");
    options.Model = "rerank-v4.0-fast";
    options.MinimumAnswerableRelevance = 0.5;
});
```

The adapter sends the complete bounded candidate set to `POST /v2/rerank` with `top_n` equal to the candidate
count. Cohere returns relevance but not answerability, so the adapter deterministically marks candidates at or
above `MinimumAnswerableRelevance` as `Answerable`; aggregate answerability is `Answerable` when at least one
candidate passes. Tune this product threshold against an evaluation set.

`Rag.Reranking.Timeout` owns request cancellation. HTTP errors, rate limits, malformed/incomplete responses, and
provider failures flow through the configured `FailurePolicy`; response bodies and credentials are not projected
to observability. Do not add retries inside the adapter: retries can exceed the agent timeout and create additional
billed searches. If the host adds resilience, bound attempts within the same timeout and retry only transient
statuses.

Cohere currently documents 10 Rerank requests/minute for trial keys and 1,000 requests/minute for production keys.
Rerank is billed in search units: one query with up to 100 documents is one search unit, while long documents may
be split and count as additional documents. Confirm current limits and prices before release:
https://docs.cohere.com/v2/reference/rerank,
https://docs.cohere.com/v2/docs/rate-limits, and https://cohere.com/pricing.

### Performance and operational acceptance criteria

The following values are initial Runiq release SLOs, not provider guarantees. Measure them per environment and
model, and tighten or relax them only from production evidence:

| Criterion | Release target |
|---|---|
| Reranking stage latency | p50 <= 300 ms, p95 <= 1 s, p99 <= 2 s over completed provider invocations |
| Hard request bound | `Timeout` defaults to 5 s; no provider work may continue after its cancellation token fires |
| End-to-end latency cost | Reranking-enabled p95 may add at most 1 s versus the paired disabled baseline |
| Success ratio | >= 99% of eligible reranking attempts |
| Fallback ratio | < 1% of eligible reranking attempts |
| Blocked failure ratio | < 0.1% of eligible reranking attempts |
| Timeout ratio | < 0.5% of provider invocations |

An *eligible reranking attempt* has `requested = true` and `candidateCount > 0`. Empty accepted-result sets are
successful no-ops and are excluded from provider reliability ratios. Classify outcomes from the content-free
metadata as follows:

- success: `outcome = Succeeded`;
- fallback: `outcome = Fallback`;
- blocked failure: `outcome = Failed`;
- timeout: `timedOut = true` or `failureCode = RerankerTimeout`.

Compute `runiq.rag.reranking.duration` as a histogram from `duration`, and
`runiq.rag.reranking.attempts` as a counter partitioned by `outcome`, `failurePolicy`, `timedOut`, agent, model,
and environment. Do not use query, document/chunk identity, content, exception text, API key, or tenant/user ID as
metric labels. Alert when a ratio breaches its target for two consecutive 15-minute windows with at least 100
eligible attempts; use a longer evaluation window for lower-volume systems. The per-execution Agent Chat payload
provides the source fields, but production aggregation should be exported by the host's metrics pipeline rather
than scraped from the Dashboard.

`MaximumCandidates` defaults to five because the default acceptance policy also selects at most five results.
That keeps the normal path complete while bounding cross-encoder latency, transmitted untrusted text, and provider
usage. Raising it is justified only when `MaximumAcceptedResults` is also higher and an evaluation demonstrates a
quality gain; lowering it is appropriate for tighter latency or data-egress budgets. Candidates beyond the bound
retain their original relative order and are not billed or transmitted by the reranker adapter.

Before enabling reranking in production, compare enabled and disabled variants on the same versioned query set,
retrieval candidates, prompts, model, and context budget. Use paired measurements and record at least:

- ranking quality (`NDCG@5` as the primary metric and `MRR@5` as a secondary metric);
- grounded-answer correctness or reviewer acceptance;
- no-context rate split by `Unknown` and `NotAnswerable`;
- reranking-stage and end-to-end p50/p95/p99 latency;
- success, fallback, blocked failure, and timeout ratios;
- provider search units and estimated cost per 1,000 agent executions.

The release gate is a >= 3% relative improvement in the chosen primary quality metric, no critical language,
tenant, document-type, or safety slice regressing by more than one percentage point, and compliance with every
latency/reliability target above. If the quality gate is not met, keep reranking disabled. If quality passes but an
operational target fails, reduce candidates, select a lower-latency model, or improve provider capacity before
release; do not hide the regression with a longer timeout.

Context selection is a separate stage after acceptance. The runtime calculates
`MaximumContextTokens - instructions - conversation history - user query - response reserve - other required prompt`
and selects only complete chunks whose final serialized external-context message fits. The deterministic fallback
estimator counts contiguous Unicode letter/digit runs and individual punctuation marks; it does not call a model
and is explicitly an estimate rather than an exact provider token count. The defaults are 32,768 maximum context
tokens and a 4,096-token response reserve. `MaximumChunksPerSource` defaults to `int.MaxValue` for compatibility;
set a bounded value to prevent one document from monopolizing context. `PreferSourceDiversity` performs stable
source rounds while retaining retrieval order within each source. Chunks are never silently truncated.

Accepted results omitted from model context remain available through `ContextExcludedResults` with
`TokenBudgetExceeded`, `OverlappingContent`, or `SourceLimitExceeded`. Overlap reduction uses character boundaries
from stable same-document chunk metadata and keeps the earlier retrieval result when at least half of the shorter
span overlaps. `ContextBudget` exposes count-only estimates and totals without exposing prompt or document text.
If mandatory prompt content plus the response reserve exceeds the maximum, execution fails with
`RagContextBudgetExceeded` before model invocation. When accepted results exist but none fit, the no-context reason
is `ContextBudgetExhausted`, and the configured grounding/no-context policy remains authoritative.

The framework keeps raw provider score semantics separate from normalized relevance. Cosine similarity in
`[-1,1]` is normalized with `(raw + 1) / 2`; non-negative Euclidean distance is normalized with `1 / (1 + raw)`.
Cosine is higher-is-better and Euclidean distance is lower-is-better. Unbounded dot product has no universal
normalization, so `Relevance` remains null. A provider-specific policy can explicitly accept such candidates:

```csharp
using Runiq.AI.Rag.Models.Search;

rag.Acceptance.ProviderSpecificAcceptance = result =>
    result.Metric == RagScoreMetrics.DotProduct && result.RawScore >= 2.5;
```

Missing metrics, inconsistent metric direction, NaN, infinity, and relevance outside `[0,1]` are retained as
`InvalidScore` rejections. Duplicate content, threshold failures, and accepted-result overflow are retained as
`DuplicateContent`, `BelowMinimumRelevance`, and `ResultLimitExceeded`. Equal relevance uses ordinal document ID
and chunk ID ordering, so identical query/index/provider configuration produces stable context order.

A successful empty retrieval reports `NoResults`; a successful retrieval whose candidates are all rejected reports
`BelowRelevanceThreshold` when every rejection is threshold-based, otherwise `CandidatesRejected`. Retrieval
exceptions remain `RagRetrievalFailed` and are never converted into a no-context result or normal answer.

The runtime emits framework grounding rules as authoritative instructions and sends accepted document text only
inside a separate `<untrusted-external-context>` user message. Document instructions are treated as untrusted
data and cannot be promoted into system, developer, agent, or framework instruction authority. This boundary is
a prompt-injection mitigation, not a guarantee that a model can never be manipulated.

`AgentExecutionResult.Rag`, terminal `AgentExecutionEvent.Rag`, and Agent Chat result responses expose
the applied mode, accepted-context status, applied no-context behavior and reason, whether model invocation was
skipped, and whether the framework constrained the answer to accepted context. The metadata also exposes ordered
candidate, accepted, and rejected collections and their counts; every item carries raw score, normalized relevance,
metric, direction, and any rejection reason. Streaming and non-streaming executions share this same evaluation.
`IsAnswerGrounded` reports the applied framework policy; it is not independent semantic verification of model output.
Agent Chat SSE projects the content-free RAG search lifecycle through dedicated `rag_search_started`,
`rag_search_completed`, and `rag_search_failed` events instead of serializing runtime result collections.

For completed searches, optional `reranking` observability is an allowlisted contract: stage metadata contains
`requested`, `ran`, `candidateCount`, `duration`, `outcome`, `failurePolicy`, aggregate `answerability`, `timedOut`,
optional safe `failureCode`, and `candidates`. Candidate metadata contains only `documentId`, `chunkId`,
`originalRank`, `rerankRank`, `rerankRelevance`, and candidate `answerability`. It never contains query text,
chunk content, provider response bodies, exceptions, stack traces, credentials, or arbitrary provider metadata.
All enum values use stable string names in Agent Chat JSON. A successful answerability rejection additionally uses
`noContextReason: "NotAnswerable"` and `contextExcludedResults[].reason: "NotAnswerable"`.

## Tool Design

A Runiq tool is a regular C# class that implements:

```csharp
IRuniqTool<TInput, TOutput>
```

This gives you:

- Strongly typed input models
- Strongly typed output models
- Testable business logic
- Clean separation between agent behavior and application code

## Typical Use Cases

Use `Runiq.AI.Agents` when you want to build:

- AI assistants for .NET applications
- Tool-using agents
- Domain-specific agents
- Agent workflows
- Internal automation agents
- Dashboard-observable agent runtimes
- MCP-compatible agent experiences

## Related Packages

Runiq AI is modular. `Runiq.AI.Agents` can be used together with other Runiq packages:

| Package | Purpose |
|---|---|
| `Runiq.AI.Core` | Hosts agents and the embedded dashboard in ASP.NET Core |
| `Runiq.AI.Rag` | Owns document-based retrieval, vector indexes, and RAG query primitives |
| `Runiq.AI.Workflows` | Orchestrates agents in code-first workflows |
| `Runiq.AI.Mcp` | Exposes ASP.NET Core applications through MCP-compatible tools |

## Documentation

Full documentation is available at:

https://runiq.net/docs

## Status

This package targets the Runiq AI 1.0.0 stable release.

The main direction is clear:

> Build code-first AI agents, tools, workflows, context sources, MCP endpoints, and dashboards for .NET applications.

## License

MIT

## Executor selection

Migration to the fluent API is optional. The existing `model:` constructor remains
supported without deprecation, including positional arguments and derived-class
`base(...)` calls. Both forms select the same model executor before returning.

```csharp
using Runiq.AI.Agents;
using Runiq.AI.Agents.Configuration;

var draft = new Agent(id: "draft", name: "Draft", instructions: "Answer questions.");
// draft.Executor is null; select an executor before registration.

var modelAgent = new Agent("support", "Support", "Answer questions.")
    .UseModel("openai/model-name");
var codexAgent = new Agent("reviewer", "Reviewer", "Review code.").UseCodex(options => options.Model = "gpt-6-astra");
var claudeAgent = new Agent("analyst", "Analyst", "Analyze.").UseClaude(options => options.Model = "sonnet");

// Existing constructor calls, including derived agent constructors, remain valid.
var existingAgent = new Agent(
    id: "support", name: "Support", instructions: "Answer questions.", model: "openai/model-name");

AgentExecutorKind kind = codexAgent.Executor!.Kind;
AgentModelConfiguration? modelSettings = modelAgent.Executor!.Model;
```

Codex requires an explicit non-blank agent-level model; parameterless selection is
not available. `ReasoningEffort` defaults to `CodexReasoningEffort.High` and
`ServiceTier` to `CodexServiceTier.Default`. Both can be overridden in the callback.
Default tier preserves local CLI settings; Fast sends `service_tier="fast"`.
`OPENAI_API_KEY` is not required: Codex uses local CLI authentication.
Runiq does not validate a model-name catalog. Invalid/unavailable models are rejected
by the CLI and normalized to `CodexModelNotAvailable`; invalid reasoning combinations
become `CodexReasoningEffortNotSupported`. Events/results retain diagnostic context
in `ErrorDetails`. See [Codex configuration and resume](../../docs/codex-executor.md).

The `existingAgent` and `modelAgent` examples above are equivalent model definitions.
Tool registration works before or after selection. This example only defines agents;
after host registration, each CLI executor exposes their tools through an automatic
per-run MCP bridge:

```csharp
using System.Threading;
using System.Threading.Tasks;
using Runiq.AI.Agents;
using Runiq.AI.Agents.Tools;

var first = new Agent("first", "First", "Review.").UseCodex(options => options.Model = "gpt-6-astra").AddTool<MyTool>();
var second = new Agent("second", "Second", "Review.").AddTool<MyTool>().UseClaude(options => options.Model = "sonnet");

/// <summary>Returns input unchanged when invoked by a supported execution path.</summary>
[RuniqTool(name: "echo", description: "Echoes input.")]
public sealed class MyTool : IRuniqTool<string, string>
{
    /// <inheritdoc />
    public Task<string> ExecuteAsync(string input, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(input);
    }
}
```

The three-argument constructor validates identity and creates a definition with no executor.
Both constructors trim `Id` and `Name` and reject null, empty or whitespace values.
`Instructions` preserves whitespace; null becomes an empty string.
`UseModel` validates the provider/model reference, reasoning effort and verbosity immediately.
Its optional API key, provider settings and generation defaults match the existing constructor.
The defaults remain `apiKey: null`, `provider: null`, `reasoningEffort: "minimal"`, and
`verbosity: "low"`. Both APIs use Core's `ModelReference.Parse`, including provider normalization
and model names containing additional slashes. Existing provider options, including named model
registrations, are retained by reference and resolved by the existing runtime.

All three selection methods return the original `Agent`, so calls can continue with `AddTool<T>()`
or `UseRag(...)`. Definition and selection only perform local validation and configuration: they
send no network requests, start no processes, and perform no authentication. Codex and Claude CLI
installations are not prerequisites for definition or registration. `UseCodex(...)` and
`UseClaude(options => options.Model = "sonnet")` configure no timeout, sandbox or session behavior; host executor options own
process settings. The model-specific `ProviderOptions.Timeout` remains unchanged.

Every successful selection is final: repeat selections, changes between executors and concurrent
second selections throw `InvalidOperationException`. Invalid model options leave selection available.
Repeat selection is rejected before validating the new options, even if the second model is invalid.
The error identifies the agent and its existing executor. Invalid first selections throw
`ArgumentException` with the agent ID, original parameter name and validation cause; the executor,
tools and RAG settings remain unchanged. A corrected selection can still complete the definition.
Selected executor configuration classes are sealed, have no public constructors or setters,
and cannot be assigned to an agent. Mutable `CodexAgentOptions` and `ClaudeAgentOptions`
callback values are validated and copied into their immutable executor configuration snapshots.
Identity, instructions, tool registrations and RAG configuration remain on the same `Agent` instance.

### Fluent definition acceptance evidence

The existing implementation is retained. Definition methods call the shared
`SelectExecutor` mechanism; model configuration reuses `ModelReference.Parse`.
`Interlocked.CompareExchange` publishes at most one validated configuration. This
guarantee applies to executor selection, not concurrent mutation of tool lists or
provider options. The definition path has no network, process, credential-validation
or runtime-service dependency. Selection configures the definition; the CLI adapters
registered by `AddRuniqServer` perform execution later.

| PBI | Acceptance evidence in `AgentExecutorTests` |
| --- | --- |
| 01 — Draft definition | `Constructors_PreserveIdentityValidation`, `Constructors_PreserveNormalization`, `Registration_ValidatesCompletedDefinitions`, `LegacyConstructor_PreservesSignatureAndOptionalDefaults` |
| 02 — Model selection | `UseModel_PreservesLegacyConfiguration`, `ModelDefinitions_AcceptExistingReferenceFormats`, `ModelDefinitions_ProduceEquivalentEffectiveRequests`, `InvalidModel_LeavesSelectionAvailable`, `InvalidModel_CanBeCorrectedWithModelSelection` |
| 03 — Codex/Claude preference | `RequestedExamples_PreserveNamedConstructorAndDefaults`, `Selection_IsExclusiveAndPreservesTools` |
| 04 — Chaining and exclusivity | `FluentSelection_AllowsFurtherChaining`, `Selection_IsExclusiveAndPreservesTools`, `LegacyConstructor_RejectsEverySecondSelection`, `ConcurrentSelections_AllowExactlyOneWinner`, `ReadmeToolExamples_OnlyRegisterDefinitions` |
| 05 — Examples and documentation | Examples above, XML comments on the draft constructor and selection methods, and the compiled example/definition tests |

The definition tests require neither credentials nor a Codex/Claude CLI. Effective
request equivalence uses an in-memory scripted chat client, not a real provider.

`Executor` is null until selection; `Executor.Model` is null for Codex and Claude. Model-provider
state lives in `AgentModelConfiguration`; CLI settings live in `Executor.Codex` or `Executor.Claude`.
Existing `Agent` model properties remain read-only aliases
with their original types and values for model agents. On non-model or incomplete definitions,
`Model`, `ModelReference`, `ProviderName`, `ModelName`, `ReasoningEffort` and `Verbosity` throw
`InvalidOperationException`; `ApiKey` and `Provider` return null. Use `Executor` when inspecting
arbitrary definitions. Codex metadata exposes its selected model and reasoning effort,
with null verbosity; Claude metadata likewise exposes its explicit model and reasoning effort,
with null verbosity.
Their metadata provider labels are `Codex CLI` and `Claude CLI`.

Safe inspection across all executor choices:

```csharp
using System;
using Runiq.AI.Agents;

Agent agent = new Agent("reviewer", "Reviewer", "Review code.").UseCodex(options => options.Model = "gpt-6-astra");
if (agent.Executor?.Model is { } model)
    Console.WriteLine($"{model.ModelReference.ProviderName}/{model.ModelReference.ModelName}");
else
    Console.WriteLine(agent.Executor?.Kind.ToString() ?? "No executor selected");
```

| Property | Model executor | Codex/Claude or no selection |
| --- | --- | --- |
| `Executor` | Selected configuration | Selected configuration, or null before selection |
| `Executor?.Model` | Model configuration | null |
| `Model`, `ModelReference`, `ProviderName`, `ModelName` | Configured/parsed values | `InvalidOperationException` identifying the agent and missing model executor |
| `ReasoningEffort`, `Verbosity` | Normalized values | Same exception |
| `ApiKey`, `Provider` | Original supplied values or null | null |

Provider settings retain reference identity; they are not copied. Invalid model and
generation settings still fail at definition time with `ArgumentException`. Historical
parameter names remain `model`, `ReasoningEffort` and `Verbosity`; error messages may
include additional agent context. Provider URL/timeout validation stays at registration.
Metadata uses safe executor access and nullable model fields; it exposes no API key
or provider configuration. CLI provider labels are display values, separate from the
`Agent.Provider` property. Its TypeScript consumer also accepts explicit null values.

### Legacy model compatibility evidence

The pre-executor public constructor and validation behavior were compared with commit
`89180ff`. No additional compatibility layer or model parser is needed.

| PBI | Evidence |
| --- | --- |
| 01 — Constructor compatibility | `LegacyConstructor_PreservesSignatureAndOptionalDefaults`, identity/normalization tests, `DerivedAgents_PreserveBaseConstructorContract`; unchanged sample projects built with the solution |
| 02 — Common selection | Constructor delegates to `UseModel`, then `SelectExecutor`; `UseModel_PreservesLegacyConfiguration` and `LegacyConstructor_RejectsEverySecondSelection` |
| 03 — Effective settings | `ModelDefinitions_ProduceEquivalentEffectiveRequests` uses a scripted client; `InvalidSettings_PreserveLegacyExceptionContract` and `ProviderValidation_RemainsAtRegistration` cover validation boundaries |
| 04 — Getter/metadata contract | `GetterAndMetadataContract_CoversEverySelection` checks Model, Codex, Claude and drafts, including serialized metadata without secrets |
| 05 — Optional migration | Examples and property table above; existing model/tool/RAG regressions and unchanged derived-agent samples |

### Validation boundaries and dependency audit

The same executor-presence check is used at registration and runtime. Definition may
start incomplete; registration may not finish with an incomplete agent. Selection
validates a candidate before atomic publication and never replaces a valid selection.
The current error priority checks an existing selection **before** validating a new
model argument. Thus a second `UseModel("invalid")` raises `InvalidOperationException`;
the same argument on a draft raises `ArgumentException` and leaves the draft reusable.

| Scenario | Boundary and contract | Acceptance evidence |
| --- | --- | --- |
| Draft creation and missing registration | Constructor accepts the draft; registration throws `InvalidOperationException` naming the agent and all three selection methods | `Constructors_PreserveIdentityValidation`, `Registration_ValidatesCompletedDefinitions`, `Registration_AllowsSelectionAfterAddingDraft`, `RuntimeOverloads_RejectBeforeAnyExternalWork` |
| First and second selection | Shared `SelectExecutor`; all nine second-selection combinations reject without replacing configuration | `Selection_IsExclusiveAndPreservesTools`, `LegacyConstructor_RejectsEverySecondSelection` |
| Concurrent valid selections | Exactly one published configuration; identity, instructions and tools remain intact | `ConcurrentSelections_AllowExactlyOneWinner` |
| Invalid first model selection and recovery | Existing parser/generation validation; no assignment until valid; Model, Codex or Claude can be selected afterward | `InvalidModel_LeavesSelectionAvailable`, `InvalidModel_CanBeCorrectedWithModelSelection`, `ConfigurationFailures_DoNotExposeSensitiveSettings` |
| Missing or unimplemented runtime selection | `AgentExecutorMissing` versus `AgentExecutorNotSupported`, before provider, retrieval or tool work | `RuntimeOverloads_RejectBeforeAnyExternalWork`, `Runtime_ExecutesFluentModel` |
| Safe and compatible errors | Preserve argument types/parameter names; redact supplied provider URL rather than echoing potential credentials | `InvalidSettings_PreserveLegacyExceptionContract`, `ProviderValidation_RemainsAtRegistration`, `ConfigurationFailures_DoNotExposeSensitiveSettings` |

The runtime guard test covers all six existing overloads: result mode by agent object
or ID with string or `AgentQuery`, and streaming by ID with either input form and a
tool-invoker override. Runtime has no agent-object streaming overload; this feature
adds none. `AddRuniqServer` registers built-in adapters for selected Codex/Claude agents.
A manually assembled runtime without the corresponding adapter fails with
`AgentExecutorNotSupported`. Custom executors remain supported by the existing registry;
CLI execution never falls back to a model provider.

Registration keeps case-insensitive duplicate-ID checks and provider URL/timeout
validation. These checks remain local and do not authenticate keys or probe endpoints.
Invalid provider URLs produce an actionable message without echoing the URL, which may
contain credentials. For example:

```csharp
using Runiq.AI.Agents;
using Runiq.AI.Agents.Validation;

var draft = new Agent("support", "Support", "Answer questions.");
// AgentValidator.ValidateRegisteredAgents([draft]) would throw: select an executor first.
draft.UseModel("openai/model-name");
AgentValidator.ValidateRegisteredAgents([draft]);
// draft.UseCodex(options => options.Model = "gpt-6-astra") would throw: the Model executor is already selected.
```

Selection is committed only after model validation succeeds. Registration and runtime share the
executor check in `AgentValidator`: registration requires a selection, while runtime additionally
requires an implemented executor. `AddRuniqServer` registers Codex/Claude infrastructure
for agents that select those kinds. Manually assembled runtimes without an adapter
retain `AgentExecutorNotSupported`.
An incomplete definition returns `AgentExecutorMissing` at runtime and an actionable exception
at registration. Neither failure consumes the unfinished definition's ability to select an executor.

| Location | Previous model assumption | Responsibility after this change |
| --- | --- | --- |
| `Agent` constructor and model properties | Every definition parses a required model | Existing eight-parameter public constructor selects model execution; new three-parameter constructor permits an unfinished definition. The former private RAG constructor was implementation-only and is removed. |
| `RuniqServerOptions.AddAgent` | Collects already constructed model agents | Still collects definitions; allows configuration to finish inside the server callback. |
| `AgentValidator` / `AddRuniqServer` | Checks duplicate IDs and provider URL/timeout | After the callback, requires an executor, rejects duplicate IDs, and retains model provider checks. Codex/Claude need no provider or key. |
| `AgentExecutionRuntime` | RAG, model resolution, endpoint, API key, generation options and chat request all assume model execution | Rejects missing (`AgentExecutorMissing`) or unsupported (`AgentExecutorNotSupported`) executors before RAG or provider resolution. Existing model API-key validation and execution order remain intact. Direct runtime definitions are checked too. |
| RAG embedding registration | Reads `ProviderName` for every RAG agent | Inspects model configuration before registering OpenAI embedding clients. |
| `RuntimeMetadataService` | Reads non-null model and generation aliases | Reads model, Codex or Claude configuration safely. Both CLI kinds expose explicit model/reasoning settings and a provider display label; existing model values are unchanged. |

Local Codex CLI execution is registered automatically by `AddRuniqServer` for
agents selecting `UseCodex(...)`; see [configuration, continuation and limits](../../docs/codex-executor.md).
Local Claude Code execution is also registered automatically for `UseClaude(options => options.Model = "sonnet")` agents; see
[configuration and continuation](../../docs/claude-executor.md). Codex agents support
existing `.AddTool<T>()` bindings through an automatic per-run loopback MCP bridge;
tool events use the existing dashboard UI. Claude agents support the same
`.UseClaude(options => options.Model = "sonnet").AddTool<T>()` pattern through that shared bridge.
Direct `Agent.ExecuteAsync` and `Agent.ExecuteStreamAsync` retain
their existing unsupported-direct-execution contract. Runtime execution continues through dependency
injection. The executor types belong to Agents and reuse Core's `ModelReference` and `ProviderOptions`;
no new dependency or parallel agent model is introduced. Workflow adapters already delegate execution
to the Agents runtime and require no changes.

## Execution lifecycle

Every runtime invocation creates a fresh `RunId`. Runtime events and results expose
that identifier together with `AgentId` and `Status`. `ProviderSessionId` carries the
confirmed Codex thread ID or Claude session ID when available and otherwise remains null; `RunId` cannot resume a provider session. Reusing an `AgentQuery`
starts a new run, including when calls overlap. Configure agents before executing
them and do not mutate their definitions during execution.

`AgentExecutionRequest` retains the agent and complete existing `AgentQuery`.
`AgentRunContext` owns invocation identity and state, separately from the RAG context.
It exposes UTC `StartedAt` and nullable `EndedAt`; runtime sets the end time together
with the first terminal state and preserves it thereafter. Streaming starts on the
first enumeration step, not when its enumerable or enumerator is obtained. Each new
enumeration receives a fresh context, even when the same enumerable is reused.
Runtime resolves registered executors and owns their lifecycle. Codex and Claude
are registered automatically for server agents selecting those kinds, with no model fallback.

States are `Running`, `Completed`, `Failed`, and `Cancelled`. Both `ExecuteAsync`
and streaming throw `AgentRunCanceledException`, an `OperationCanceledException`,
for caller cancellation. Its `Run` property identifies the cancelled invocation.
Cancellation produces no terminal event or result. Disposing an unfinished stream
cancels the run and releases the executor. Streams are lazy, and enumerating one
again creates a new run. Unexpected executor failures and responses containing neither
text nor explicit JSON produce `Failed` consistently in both APIs. Waiting for interactive
approval and resuming a paused approval are not supported. CLI conversation continuation
uses `AgentQuery.ProviderSessionId` from a previous successful result.

Published events include a one-based `SequenceNumber` and UTC `Timestamp`. Successful
completion events include the final `Message` and optional `StructuredOutput`, matching
the aggregated result. `StructuredOutput` is a `JsonElement?` cloned by the factories;
dispose the source `JsonDocument` freely after creating the event or result. Text is
never parsed automatically. Supply JSON explicitly with
`AgentExecutionEvent.Completed(rag, citations, structuredOutput)` or the five-argument
`AgentExecutionResult.Success` overload. JSON-only success has an empty `Message`.
The result builder rejects incomplete correlated streams, including cancelled streams.

Register custom executors with `services.AddScoped<IAgentExecutor, YourExecutor>()`.
Each implementation exposes its `AgentExecutorKind Kind` and the single event-stream
`ExecuteAsync` method. Runtime continues to own identity, ordering, error handling and
cancellation for both public APIs. Custom executors override the built-in Codex/Claude
fallbacks regardless of whether they are registered before or after `AddRuniqServer`.
The scoped resolver rejects multiple custom registrations for the same kind when
resolved, rather than choosing the last. The built-in model
registration is idempotent across repeated hosting registration. Missing selection
returns `AgentExecutorMissing`; missing implementation returns `AgentExecutorNotSupported`.
`AddRuniqServer` automatically registers Codex and Claude when selected by its agents.
No separate executor registration is needed. `WorkingDirectory` defaults to the host
content root (current directory without a host environment). Optional process settings
use `services.Configure<CodexExecutorOptions>(...)` or `Configure<ClaudeExecutorOptions>(...)`.
No model fallback is performed.

Existing overloads and result/event factories remain available; standalone factory
products have null run identity. See the repository's
[lifecycle and compatibility decisions](../../docs/agent-execution-lifecycle.md)
for the full compatibility table, including the RAG `ConversationId` migration.

Agent Chat HTTP results also carry `runId`, `agentId`, string `status` and optional
`structuredOutput`. SSE retains its event types and adds run identity, status,
`sequenceNumber` and UTC `timestamp`; successful terminal frames also carry the full
`message` and explicit JSON. A failed tool step remains a `Running` run event.
Cancellation produces neither a terminal frame nor `[DONE]`. The lifecycle document's
[HTTP/SSE compatibility section](../../docs/agent-execution-lifecycle.md#hosted-http-and-sse-compatibility)
describes field omission, JSON-only output and transport behavior. Clients with strict
schemas should allow these additive fields. Separate API invocations create distinct
runs; aggregate a stream with `AgentExecutionResultBuilder` to obtain its own result.

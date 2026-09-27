# Programmatic multi-turn Memory and shared context budgets (#201–#202)

The built-in model executor now continues an authorized conversation across separate calls.
Enable the agent with `.UseMemory()`, explicitly register `AddRuniqMemory` and either
`AddRuniqMemoryInMemory` or `AddRuniqMemoryPostgreSql`, and supply trusted scoped
`IMemoryIdentityResolver` / `IMemoryAccessPolicy` adapters. Resolve `AgentExecutionRuntime`
from a DI scope. Package availability alone enables nothing; disabled execution resolves
no Memory services. Native Codex/Claude provider-session continuation remains independent.

Run the complete, credential-free example:

```powershell
dotnet run --project samples/Runiq.AI.MemoryConversation
```

[Example composition](../samples/Runiq.AI.MemoryConversation/Program.cs) and
[the two-call flow](../samples/Runiq.AI.MemoryConversation/Services/ConversationDemo.cs)
compile as part of the solution. A deterministic Core model client verifies the history
boundary without a paid model call. Replace that client registration with your configured
model provider for live usage; keep the same Memory composition.

```csharp
var first = await runtime.ExecuteAsync("support", new AgentQuery("My name is Ada.")
{
    Memory = new MemoryReference("project-42"),
    MemoryTurnId = "introduction"
});
if (!first.IsSuccess) throw new InvalidOperationException(first.ErrorCode);

var next = new AgentQuery("What is my name?")
{
    Memory = new MemoryReference("project-42", first.ThreadId),
    MemoryTurnId = "recall-name"
};
await foreach (var item in runtime.ExecuteStreamAsync("support", next, cancellationToken: cancellationToken))
{
    if (item.Kind == AgentExecutionEventKind.ConversationStarted)
        Console.WriteLine(item.ThreadId);
    if (item.Kind == AgentExecutionEventKind.AssistantDelta)
        Console.Write(item.Content);
}
```

The example snippet assumes imports from `Runiq.AI.Agents`, `.Runtime`, and
`Runiq.AI.Memory.Models`, plus a registered `support` agent and trusted host adapters.
`MemoryContext` is internal execution state, never a client authorization capability.
Every call resolves trusted identity and authorizes ownership before requesting content;
every store operation rechecks authoritative scope and current access policy.

## Identity and observable creation

| Identity | Meaning |
| --- | --- |
| `ThreadId` | Persistent tenant/resource/agent-bound conversation; return it with subsequent calls. |
| `AgentQuery.MemoryTurnId` / `MemoryTurn.TurnId` | Caller-retained logical request identity, scoped to the thread; defaults to the fresh RunId. |
| `RunId` | New invocation identity on every call, including a rejected duplicate; associates persisted messages with the owning turn. |
| Message ID / append key | Stable generated identifiers within a turn session; retries keep the original instances, timestamps, payloads, and expected versions. |
| `ProviderSessionId` | Native CLI continuation; never a Memory identifier. |

With a null thread reference, authorization proposes an ID and the selected provider
atomically persists ownership. The first programmatic `ConversationStarted` event is
published **after creation and before turn reservation, history loading, RAG, or model work**.
It is also published for an authorized existing thread. Its `ThreadId` is reusable; it does
not claim the user input or a turn was committed. Every later event and the aggregated
result retain that ID, including failures. Cancellation throws `AgentRunCanceledException`;
its `Run.ThreadId` retains a confirmed creation even when no terminal result is returned.
If creation itself has an uncertain commit and no receipt was observed, no confirmed ID
is claimed. Persist the first event promptly if disconnect recovery matters.

Disposing immediately after `ConversationStarted` leaves a valid empty conversation with
no turn. The first user write has not happened yet. A null ThreadId always requests a new
conversation: retaining a turn ID alone cannot deduplicate two newly created threads.

## Write order, replay, and atomicity

1. Authorization and persisted ownership precede content access.
2. A `Running` turn and exactly one user message commit together at the observed version.
   That version becomes the stable history boundary. The store rejects overlapping turns
   or stale versions before any model/tool execution.
3. Memory loads **all** message pages through that boundary, admitting only completed turns.
   Before each model invocation, the shared assembler selects bounded, complete history groups.
   The current user input is added once at the model boundary. Instructions and transient
   RAG evidence are separate and never copied into the persisted transcript.
4. Streaming text is accumulated per model round. Before invoking tools, the assistant
   message (including its text and ordered calls) is appended. Each actual tool result is
   appended in order, including the exact failure JSON supplied to the model. Tool IDs,
   names, original argument strings, timestamps, and run associations survive round trips.
5. On success the final accumulated assistant message and `Completed` turn snapshot commit
   atomically with the retry receipt **before** the runtime publishes `Completed`.
   Chunks are never stored as individual completed messages. Terminal-only appends may be empty.

Previous tool calls/results are replayed as Core messages, never executed again. A tool
failure that is returned as a model-facing result may be handled by the model and the run
may still complete successfully. A thrown/cancelled tool or runtime failure has a failed
or cancelled turn outcome. Completion cannot commit with unresolved tool calls.

All messages associated with `Failed`, `Cancelled`, or `Running` turns are excluded from
future model context, including their user input. Partial text and pending calls remain
available for authorized diagnostics. Legacy #200 messages without turn state also have
unknown completion status: storage reads/retries preserve them, while runtime replay
excludes them. An interrupted process leaves `Running`, with no fabricated terminal timestamp.

Conversation version remains the last message sequence from #200. An empty terminal
append does not increment it; its receipt represents `[Version + 1, Version]`. Lifecycle
validation and the reservation protect state-only transitions. In-memory performs these
operations under the existing entry gate; PostgreSQL uses its existing conversation row
lock and transaction across independent processes. No SQL or ownership policy is duplicated
in Agents. Migration `002_turns.sql` adds versioned turn JSON without changing message JSON
v1 or existing append receipt payloads. Apply migrations explicitly before upgrading hosts.

## Retry and concurrency policy

Only one running turn may own a thread. Overlap is rejected with `MemoryTurnConflict`, or
`MemoryVersionConflict` when the competing reservation observes a stale version. Calls in
different authorized threads remain independent. The service checks logical turn reuse;
the store enforces ownership, unique turn/run associations, and transitions atomically.
Direct legacy appends are also rejected while a turn is active.

Reusing a retained `(ThreadId, MemoryTurnId)` is **rejected**, even after completion and even
when the input matches. Changed input is also rejected. This API does not resume execution,
return a cached model result, or promise exactly-once external tool side effects. Omitting
MemoryTurnId makes each invocation a new logical request; the default identity is its RunId.
Use a new logical ID only when intentionally asking for another turn.

A store `StorageFailure` may mean an uncertain commit. The session retries that exact
immutable append once with the same token. It does not regenerate keys/timestamps, refresh
the expected version, or rerun model/tool work. Other conflicts are not retried. If a
repeated attempt fails, the caller sees a safe failure and server diagnostics retain the
underlying exception. Runtime captures the session through the `BeginAsync` recovery overload
before the initial append, so cancellation after commit or two lost startup acknowledgements
cannot discard its context or original pending request. During cleanup, an uncertain pending
append must be reconciled using that same request before a different terminal transition is attempted. A terminal write
may already have committed even if the caller received failure/cancellation; inspect the
authorized durable state rather than treating an uncertain result as permission to replay tools.

A process crash leaves a running reservation that blocks subsequent turns. There is no
lease timeout or automatic takeover. After establishing that the original worker is gone,
an authorized host may inspect the stored turn and explicitly append its Failed/Cancelled
transition using the current expected version and original turn identity. Preserve the
original timestamp, run ID, and history boundary. Do not edit SQL rows or re-execute tools
to clear a conflict. Automated recovery/retention policy is outside #201.

## Cancellation and persistence failures

Both APIs share the same lazy event orchestration. Cancellation before enumeration performs
no Memory writes. Cancellation during output or tool execution, and early stream disposal,
never synthesize a Completed event. Executor resources are disposed, then finalization uses
an independent cancellation token with a **five-second timeout**. Cooperating store/policy
adapters must honor it. Partial text and the Cancelled state are written atomically when
possible. A failed cleanup is logged with run/agent correlation; primary caller cancellation
is preserved, and an uncommitted terminal state remains distinguishably Running.

Read, append, and terminal-write failures use safe `Memory*` error codes (for example
`MemoryStorageFailure`, `MemoryTurnConflict`, `MemoryVersionConflict`, `MemoryAccessDenied`),
never raw database or identity diagnostics. Streaming consumers may have received deltas,
but only a Completed terminal event proves successful durable completion. A disconnected
consumer cannot receive a new failure event; the server log records cleanup failure.
In-memory data is volatile for the DI-container lifetime. PostgreSQL data survives host
and process restarts; tests start separate executables to verify both continuation APIs.

## Shared bounded model context

Configure one window on the agent independently of RAG:

```csharp
using Runiq.AI.Agents;
using Runiq.AI.Agents.Configuration;
using Runiq.AI.Memory.Configuration;

var agent = new Agent("support", "Support", "Help the user.", "openai/gpt-4o", apiKey)
    .UseMemory(new MemoryOptions(history: new MemoryHistoryOptions(
        maximumMessages: 40, maximumTokens: 8_000)))
    .UseContextBudget(new AgentContextBudgetOptions(
        maximumContextTokens: 32_768, responseTokenReserve: 4_096));
```

`apiKey` comes from host configuration. Register this agent with the same explicit Memory
provider and trusted adapters described above. `UseContextBudget` enables neither Memory
nor RAG and resolves no Memory services for disabled agents. It applies to the built-in
model executor; native CLI continuation is unchanged.

Effective total/reserve precedence is **explicit `UseContextBudget` > enabled
`Rag.ContextBudget` > defaults (32,768 / 4,096)**. The explicit pair overrides both RAG
values, never creates another window. RAG source-diversity and per-source limits remain
effective. A positive total and a nonnegative reserve strictly smaller than that total
are required. Invalid RAG settings remain configuration errors even when the shared
window overrides them. History message/token limits both default to null (no additional
limit beyond the shared allocation); zero excludes all history, negatives are rejected.

Every model request, including every tool continuation, uses this deterministic priority:

1. Agent and framework instructions, tool names/descriptions/schemas, the current user
   input, all active-turn assistant/tool messages, and the response reserve are mandatory.
2. Accepted RAG evidence uses the existing acceptance/rerank order, deterministic source
   rounds when enabled, overlap removal and maximum-chunks-per-source rules. Complete
   formatted chunks that do not fit are skipped, retaining explicit exclusion reasons.
3. Newest eligible history groups use the remainder under **both** configured history
   limits. Oversized groups are skipped and older fitting groups may still be selected.
   Output retains original chronological order. A historical assistant call and all its
   contiguous results are indivisible; orphan, duplicate or incomplete relationships are
   excluded. Content and JSON are never truncated or rewritten. Selecting no history is valid.

Memory history is never RAG evidence or a citation source. Only currently selected RAG
chunks can satisfy Required grounding. Source numbers stay fixed across model rounds;
removing source `[1]` can leave `[2]` as the first remaining source. `AgentCitation.Number`
is therefore independent of its zero-based `ContextOrder`. Citation markers are validated
in the round that produced them; earlier invented markers cannot become valid later.
Terminal citation metadata excludes sources absent from the final context. Earlier
streamed text remains unchanged. Source mapping and RAG metadata describe the final
attempted assembly, while the retrieval-completed event describes the initial assembly.

### Accounting and errors

Accounting is **estimated**, not tokenizer-based: `EstimatedUnicodeRuns` counts contiguous
Unicode letters/digits as one unit and individual punctuation marks as one unit. Message
framing plus role costs five units; tool-result framing costs two plus its call ID; each
assistant call adds four plus ID/name/argument costs. Tool definitions use their serialized
Core JSON representation. Evidence includes the actual escaped JSON, delimiters, citation
labels and user-message framing. All contributors use this one policy; Memory receives
per-message costs and its remaining allocation rather than inventing an estimator.

OpenAI reasoning tool continuations retain the complete ordered provider output, including
encrypted reasoning, as an invocation-local opaque snapshot. This replaces that assistant
message's wire representation without duplicating its calls or text. Its cost is message
framing plus the greater of the serialized snapshot estimate and reported output tokens
(when available), so hidden reasoning also contributes to the mandatory budget. Overflow
stops the next call; no response-ID chain restores excluded history. The snapshot is not
added to the durable Memory transcript.

For each successful assembly, estimated prompt cost plus reserve is at most the window.
This is not a guarantee against a provider's actual tokenizer limit: long words, some
languages, provider-specific framing and tokenization can differ substantially. Choose
headroom accordingly. There is no tokenizer integration or automatic model-window discovery.
The reserve allocates capacity; it does not configure a provider output-token cap.

Exact fits are accepted. Mandatory overflow returns `ContextBudgetExceeded` (or the existing
`RagContextBudgetExceeded` when RAG is enabled), with safe estimated counts and limits,
before that model call. Initial overflow makes zero calls; overflow after tools makes no
further calls. If evidence alone cannot fit, `ContextBudgetExhausted` and the existing
no-context policy apply, including Required behavior. History cannot replace evidence.
Grounded fallback instructions are recounted when no evidence remains.

`AgentExecutionEvent.ContextBudget` and aggregated `AgentExecutionResult.ContextBudget`
report the relevant/latest attempted invocation number, accounting mode, effective window,
reserve, mandatory/evidence/history costs and exclusion counts, including Memory-only
execution. Structured `ModelAgentExecutor` logs record each assembly using counts and run
identity only; they do not log transcript, tool arguments/results or evidence text. Existing
RAG observability policies remain separate. Requests send the complete selected projection
without a provider `previous_response_id` chain, which could otherwise restore excluded text.
Empty completions and provider failures before the first delta retain the latest model
call's budget diagnostics. Runs that never reach model assembly or invocation do not
inherit diagnostics from an earlier run.

Selection performs no store writes or deletions. The full active transcript is persisted
as before, even if a later budget check fails. Budget overflow finalizes a Failed turn;
cancellation/disposal preserves the existing Cancelled semantics. Neither scenario
fabricates successful completion. History is loaded once using the already authorized
reserved boundary, never reloaded under a different thread while recomposing context.

## Ownership and future handoffs

- `Memory/Models`, `Services`, `Validation`, and existing stores own neutral turn state,
  history paging, append ordering, replay selection, and lifecycle validation.
- Agents runtime owns invocation/event lifecycle and model-boundary Core message mapping.
  The built-in model executor owns exact tool transcript capture. Foundation support on a
  custom executor does not by itself implement that executor's history/tool integration.
- PostgreSQL owns SQL, turn format version, migration and transaction implementation.
- #202 supplies neutral bounded history selection in Memory and one shared model-window
  assembler in Agents, extending the existing RAG selector. Core gains no Memory dependency.
- Future #205 working-memory, #207 recall and #208 summaries must join this same allocation
  path. No contributor registry, new provider or future-feature contracts are introduced here.
- #203 owns hosted API/DTOs, thread/turn identity and conflict transport mapping, Dashboard UX,
  and the hosted sample. Programmatic fields/events here do not add those HTTP contracts.
- #204 retention/deletion must account for turn state, messages, retry receipts, and unfinished
  reservations. This release does not introduce deletion or automatic stale-turn recovery.

## Validation ownership

`Memory.Tests` owns neutral services and linked shared provider behavior. The same scenarios
run in `Memory.PostgreSql.Tests` against the repository Docker database, including lifecycle,
exact retries, cancellation, relationship validation, upgrade preservation, and process
concurrency. `Agents.Tests` covers deterministic conversation/tool replay, isolation,
failures, cancellation/disposal, duplicate invocation, and disabled/native CLI regressions.
No paid live model call is needed. See the completion report for actual current counts;
historical test totals are not acceptance criteria.

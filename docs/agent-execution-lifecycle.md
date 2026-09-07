# Agent execution lifecycle (v1)

## Scope and ownership

A run is one execution started by one runtime invocation. Streaming is lazy: a run
starts on the first enumeration, and enumerating again starts a new run. Runtime
creates one opaque `RunId`, independently of the prompt. `AgentId` identifies the
reusable definition; `ProviderSessionId` is reserved for a future provider session
and is null in v1. A `RunId` is never a session-resumption key. Provider response
IDs used inside a model/tool loop remain private to that executor.

`AgentExecutionRequest` pairs the existing `Agent` and `AgentQuery`, retaining
the complete query (message and index override) rather than duplicating it.
`AgentRunContext` belongs to runtime, separately from the RAG-only
`AgentRuntimeContext`. No run state is stored on the shared agent or query.
Agent definitions are configured before execution and must not be mutated while
in use. Reusing an agent or query concurrently creates independent run contexts.

`AgentRunContext.StartedAt` records UTC wall-clock time when runtime creates the
context. Creating a streaming enumerable or obtaining its enumerator does not create
a context; the first `MoveNextAsync` starts the run. Every new enumeration, including
overlapping enumerations of the same enumerable, receives its own identity and times.
The original agent and complete query (including `IndexName`) are passed to the
executor without copying or dropping options.

`EndedAt` is null while Running. Runtime assigns it together with the first terminal
state, including failure, cancellation and early disposal. State and end time are
published under the same lock, so observing a terminal status cannot be followed by
a missing end time. Later terminal attempts change neither field. Separate property
reads are not a transactional snapshot: a Running read can be followed by a populated
EndedAt if completion occurs between reads. Timestamps use the system UTC clock and
are not a monotonic duration clock. They belong to the run context; event `Timestamp`
continues to describe event publication, not run start or end.

`AgentRunContextTests` covers timestamp ownership, all terminal outcomes, deferred
start, lossless requests, repeated enumeration, terminal races and simultaneous
enumerations where cancellation of one leaves the other running. Existing lifecycle
and executor contract tests cover shared identity in aggregated results and isolated
tool events. No SDK, process or provider-session dependency is added to the context.

## State, events and results

States are `Running`, `Completed`, `Failed`, and `Cancelled`. Every started run
reaches exactly one terminal state; the first terminal transition wins. There is
no approval-waiting state, persistence, session continuation, or resume guarantee.
Runtime owns lifecycle and executor resolution; the model executor retains the
existing provider-neutral model, RAG and tool orchestration. No Codex or Claude
adapter is bundled; unregistered kinds retain the unsupported error code with no model fallback.

Every runtime event and returned result carries the same run identity. Existing
event ordering is retained; no start event is inserted. A fully consumed normal
stream ends with one Completed or Failed event. Empty/whitespace-only model output
is Failed in both APIs unless the executor explicitly supplies structured output.
Tool failures are steps, not terminal run failures.
Unexpected executor exceptions become a Failed event/result; existing specific
validation and RAG error codes are retained. Invalid null arguments remain caller
errors before a run starts. Missing agents and empty inputs are failed runs.

Hosting registers the model executor and executor resolver as scoped services.
The runtime's DI constructor receives the resolver and its typed logger. Execution
and cleanup failures retain their original exception and stack trace in server-side
structured logs with `RunId` and `AgentId`; response messages remain generic.
Cleanup failures during cancellation are logged without replacing `Cancelled`.
The existing public constructors remain available for manual construction and use
a null logger because their signatures do not accept a logging service.

Both APIs preserve exception-based cancellation: caller cancellation throws
`AgentRunCanceledException` (an `OperationCanceledException`) containing the
cancelled run context and the caller token. Neither API returns a success/failure
result or emits a terminal event for cancellation. A provider cancellation without
caller-token cancellation is a failure, not caller cancellation. Cancellation
observed before terminal publication wins; cancellation after a published terminal
state does not alter it. Partial stream events retain the cancelled run's identity.
Disposing an unfinished enumeration transitions its context to Cancelled and
disposes the executor; no event can be delivered to a consumer that stopped
enumerating. Cleanup errors do not create a second terminal transition.

## Compatibility decisions

| Existing surface | v1 decision |
| --- | --- |
| ExecuteAsync(agent ID or Agent, string or AgentQuery) | Keep signatures and constructor overloads; forward to one lifecycle path. |
| ExecuteStreamAsync(agent ID, string or AgentQuery, optional tool invoker) | Keep signatures, lazy enumeration, tool override and event ordering. |
| Success/Failure result factories | Keep every signature and payload, including permissive standalone Success(""). Standalone products have null run/agent/session identity and null StartedAt/EndedAt; no identity or time is invented. Runtime enforces the empty response rule. |
| Result Status and IsSuccess | Status is terminal only. IsSuccess is true exclusively for Completed; Failed and Cancelled are false. |
| Cancelled result factory | Add Cancelled(steps, rag) for standalone cancellation representation. Runtime APIs continue throwing AgentRunCanceledException on caller cancellation. |
| Event factories and existing event consumers | Keep factories and enum numeric values. Add read-only identity and status properties; Completed/Failed remain terminal event kinds. |
| AgentExecutionResultBuilder | Retain legacy uncorrelated-event support; propagate runtime identity and reject mixed runs. |
| Cancellation handlers | Existing OperationCanceledException handlers continue to work for both APIs; the subtype adds run context. |
| Previously escaping executor exceptions | Normalize into Failed with AgentExecutionFailed; caller cancellation remains an exception. |
| Empty streaming output | Now Failed (AgentExecutionEmptyMessage), consistent with ExecuteAsync. |
| RAG ConversationId | Keep the wire field, populated from runtime RunId. Reusing a query no longer reuses this identifier; it is not a provider session. |

Real Codex/Claude adapters, tool bridges, persistence, Studio and workflow changes
are outside this change.

## Shared output and executor registration

The model loop lives only in `ModelAgentExecutor.ExecuteAsync`. Runtime performs
dispatch, correlation, cancellation and terminal publication, then builds batch
results from those same events. A tool continuation is a second provider request
inside the same run, not a second runtime execution. Provider resolution, model
options, endpoints, RAG preparation and citation processing stay in the existing
model pipeline.

`ModelLoop_PreservesSingleToolInvocation` covers legacy `model:` and fluent
`UseModel(...)` definitions through batch and streaming APIs, using both public
manual runtime constructors and hosting DI. Each case requires exactly two provider
streams (initial request plus tool continuation), one tool invocation and matching
resource disposal counts. The controlled client's non-streaming completion method
throws if a separate batch model path is attempted. Existing model, RAG, citation
and provider tests cover the retained pipeline behavior.

`StructuredOutput` is an optional `JsonElement` on the existing completion event
and result. Factories clone explicitly supplied JSON immediately; source documents
may then be disposed. Undefined elements are rejected, while JSON null is a valid
explicit output. Text is never parsed to infer structured output. JSON-only success
has an empty `Message`; empty text without JSON remains a failure. Existing factory
signatures remain available and produce no structured output.

Structured output presence does not assert schema validation. It only means the
executor supplied a defined JSON value. Both the event factory and result factory
clone that value, so aggregation is safe after the source document is disposed.

Runtime results carry `StartedAt` and `EndedAt` copied from the run context through
the published events. All events share the run start time; only terminal events have
an end time. These are UTC wall-clock values, separate from event publication
`Timestamp`. A builder reconstructs the same timestamps without generating new
ones. Standalone legacy factories leave these fields null. These additions apply
to the core execution models; HTTP/SSE transport DTO fields are unchanged here.

On failure, `Message` remains null and existing tool/RAG/error steps are retained.
Partial assistant text remains in a FinalAnswer step with Failed status rather than
being presented as a completed answer. A completed tool step still records that
individual tool's success even when the overall run fails. The standalone Cancelled
factory preserves supplied steps and RAG information, returns null Message/JSON and
uses `AgentExecutionCancelled`; it does not convert incomplete streams into results
or change exception-based caller cancellation.

Runtime events receive a one-based `SequenceNumber` and UTC `Timestamp` when
published, including validation failures. Standalone factory events have null
sequence and time. A completion event's `Message` is the final accumulated text;
its structured output and terminal status are the same values used by the result
builder. `Content` keeps its existing delta/error semantics. Tool calls remain
identified by their opaque, case-sensitive call IDs, independently of tool names.
The builder refuses incomplete correlated streams and further events after their
terminal event. It rejects mixing correlated and uncorrelated events in either
direction before changing its state. Legacy uncorrelated factory-event aggregation
remains supported. Before publishing a failed terminal event, runtime substitutes
`AgentExecutionFailed` for an omitted error code so streaming and aggregate results agree.

Publication always replaces the outer event's RunId, AgentId, SequenceNumber,
Timestamp, StartedAt and EndedAt with runtime-owned values, even if an executor
replays an event from another run. The source record is not mutated. Sequence
numbers increase strictly within each run and restart at one on the next run;
there is no global ordering guarantee across concurrent runs. UTC timestamps are
wall-clock observations, not a substitute for sequence ordering.

A fully consumed, non-cancelled stream ends with exactly one Completed or Failed
event. Runtime disposes the source before publishing that terminal event and never
advances it again, including when further deltas or terminals were queued. Closing
without a terminal produces AgentExecutionProtocolError. Caller cancellation keeps
the documented exception behavior. Early consumer disposal cannot deliver a terminal
event, and server-side completion is not proof that an HTTP/SSE client received it.
Assistant, tool and typed RAG events retain their existing payload contracts; no
untyped provider-event payload or global event bus is introduced.

`IAgentExecutor` is public and exposes its `AgentExecutorKind` plus one event-stream
execution method; it does not own lifecycle transitions. Hosts register implementations
with `AddScoped<IAgentExecutor, TExecutor>()`. The scoped resolver indexes all registered
implementations by kind and rejects duplicates (including identical registrations)
with an explicit configuration exception when resolved. Built-in model registration
is idempotent across repeated hosting registration; it never silently replaces a
custom model registration. Replace the model interface registration explicitly if
that is intended. No singleton registry captures scoped executor instances.

All six public runtime overloads share the same dispatch pipeline: four batch
overloads accept an agent definition or ID with text or an `AgentQuery`, and two
streaming overloads accept an ID with text or a query. Each call invokes only the
executor selected by `Agent.Executor.Kind`; batch execution aggregates that same
event pipeline. The legacy public constructors remain available for manual model
execution, while host registration supplies the scoped resolver through DI.
Resolve the runtime inside a host scope and finish consuming its streams before
disposing that scope. The container owns registered executors and their scoped
dependencies; runtime owns each invocation's event enumerator.

`Registry_DispatchesEachRegisteredKind` exercises all six overloads against hosted
controlled Model, Codex and Claude executors. `Registry_PreservesScopedDependencies`
checks reuse within a scope, isolation across scopes and dependency disposal. Missing
selection returns `AgentExecutorMissing`; an absent implementation returns
`AgentExecutorNotSupported` before RAG, provider or tool execution. Duplicate kinds
are configuration errors at runtime resolution, rather than per-run failure results.

Missing selection is `AgentExecutorMissing`. A selected kind without a registered
implementation is `AgentExecutorNotSupported`; default Codex and Claude selections
therefore remain unsupported without model fallback. An explicitly registered
implementation is dispatched by kind, before any built-in RAG or provider work.
Executor startup, iteration and cleanup failures use the existing generic failure
contract and correlated server logging. Cancellation retains the exception-based
contract above, including for custom executors.

## Model boundaries and resource ownership

The built-in `ModelAgentExecutor` contains the only model invocation loop. Legacy
`model:` construction and `UseModel(...)` select this same executor. Batch execution
aggregates the runtime stream once; tool continuations reuse the existing provider
resolver, model options, response IDs, tool invoker, RAG context and citations.
No automatic retry is performed, including after a tool side effect or cleanup failure.

Cancellation is cooperative. The caller token flows to retrieval, reranking, provider
streams and tools. Checks before each external operation and after awaited operations
prevent subsequent calls when a dependency returns normally after cancellation.
Already-running dependencies must honor their token to stop promptly; the runtime
does not forcibly terminate them. Synchronous reflection-wrapped tool cancellation
is unwrapped and preserves the cancellation contract.

The invoker owns each tool instance it activates and disposes it once, preferring
`IAsyncDisposable` over `IDisposable`. Injected dependencies remain owned by their DI
scope. Cleanup errors are logged; a secondary cleanup error does not replace an
existing cancellation or invocation exception. Provider stream enumerators and
executor enumerators are also disposed on completion, failure and early abandonment.

An executor that ends without a terminal event produces `AgentExecutionProtocolError`,
never implicit success. Runtime disposes the executor before publishing a terminal
event and checks cancellation again after cleanup. Publication means yielding to the
enumerating caller, not acknowledgement by a transport or remote client. An abandoned
stream receives no synthetic terminal event and there is no delivery guarantee.

## Hosted HTTP and SSE compatibility

The Agent Chat request shape, routes, response modes and existing response fields
remain unchanged. The hosted result and stream DTOs project the common execution
contract; clients do not select a separate endpoint for each executor kind.

| Surface | Additive fields | Existing behavior retained |
| --- | --- | --- |
| Result JSON | `runId`, `agentId`, `status`, optional `structuredOutput` | `isSuccess`, `message`, error fields, steps, citations, grounding evidence and readiness |
| Every runtime SSE event | `runId`, `agentId`, `status`, `sequenceNumber`, `timestamp` | Existing `type`, `content` and tool/RAG payloads |
| Successful terminal SSE event | `message`, optional `structuredOutput` | `type: "completed"`, `content: null`, optional citations |

Run status is serialized as `Running`, `Completed` or `Failed`, independently of
the existing lower-case tool-step status. The new transport fields support standard
`JsonSerializer.Deserialize` through `JsonInclude`, despite their internal init
accessors. Deserialize HTTP camel-case payloads with `JsonSerializerDefaults.Web`;
SSE fields declare their wire names and also support default serializer options.
A `tool_call_failed` event still has run
status `Running`; the executor may subsequently complete successfully. Sequence
numbers start at one for each run, and timestamps use UTC. Standalone legacy events
without runtime identity omit the new run metadata. New nullable fields are omitted
when absent. Explicit JSON null is serialized as `"structuredOutput": null`; no
output omits that property entirely. Explicit output is never inferred from text,
and the JSON remains valid after the executor's source document has been disposed.
JSON-only success has `message: ""` and need not emit an `assistant_delta` event.

SSE retains `data: <JSON>\n\n` frames and the final `data: [DONE]\n\n` marker.
The marker follows either `completed` or `failed`; it means the stream finished,
not that execution succeeded. Existing clients that ignore additional properties
continue to consume the same event types. Clients with closed JSON schemas must
allow the fields listed above. No new Studio screen or tracking service is required.

The handler retains HTTP 200 for executed result-mode responses, including execution
failures; clients inspect `isSuccess`, `status` and error fields. Its existing empty
message check returns HTTP 400 with `MessageRequired` before starting a run and has
no run metadata. MVC request-model validation may also reject malformed requests
before the handler. SSE execution failures remain `failed` frames on the open stream.
Missing selection is `AgentExecutorMissing`; unregistered Codex/Claude is
`AgentExecutorNotSupported`; missing terminal notification is
`AgentExecutionProtocolError`. Duplicate executor registrations are DI configuration
errors during resolution, not per-run terminal responses.

Caller cancellation propagates `AgentRunCanceledException` through the handler in
both modes. It produces no result DTO, `cancelled` frame, or `[DONE]` marker; any
already-written deltas may be visible. The transport or host controls the resulting
disconnect/HTTP handling, so no special cancellation status code is promised here.
An SSE write failure disposes the paused runtime enumerator. An unfinished run becomes
Cancelled without further executor events. Runtime publication is not a network
delivery acknowledgement, including if writing an already-published terminal fails.

Calling streaming and batch APIs separately creates two runs. To compare terminal
information for the **same** run, apply that stream's events to
`AgentExecutionResultBuilder`; do not execute the request again to retrieve its result.
Tool-call identity across simultaneous runs is the pair `(runId, toolCallId)`.

## Offline contract coverage

`AgentExecutionContractTests` uses controlled Model/Codex/Claude executors to verify
dispatch, output ownership, per-run ordering, terminal/result equality, failures,
cancellation, duplicate registrations, missing selections, scoped lifetimes and
overlapping executions with identical tool-call IDs. The overlap test uses a barrier
so both runs are active before either tool finishes; it does not depend on sleeps.

`AgentChatExecutionContractTests` exercises the registered handler, existing mapper,
result builder and SSE serialization with controlled executors. It covers text,
JSON-only and JSON-null success, tool-step failure, explicit failure, thrown errors,
protocol errors, pre-cancellation, mid-stream cancellation, selection failures and
disconnected writers. Existing RAG hosting regressions continue to verify citations,
grounding evidence and readiness projection. These tests require no external model
service, Codex/Claude process, or credentials.

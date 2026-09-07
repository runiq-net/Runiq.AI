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

## State, events and results

States are `Running`, `Completed`, `Failed`, and `Cancelled`. Every started run
reaches exactly one terminal state; the first terminal transition wins. There is
no approval-waiting state, persistence, session continuation, or resume guarantee.
Runtime owns lifecycle and executor resolution; the model executor retains the
existing provider-neutral model, RAG and tool orchestration. Codex and Claude
remain unsupported, with their existing error code and no model fallback.

Every runtime event and returned result carries the same run identity. Existing
event ordering is retained; no start event is inserted. A fully consumed normal
stream ends with one Completed or Failed event. Empty/whitespace-only model output
is Failed in both APIs. Tool failures are steps, not terminal run failures.
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
| Success/Failure result factories | Keep every signature and payload. Standalone factory products have null run/agent/session identity; runtime stamps identity. |
| Event factories and existing event consumers | Keep factories and enum numeric values. Add read-only identity and status properties; Completed/Failed remain terminal event kinds. |
| AgentExecutionResultBuilder | Retain legacy uncorrelated-event support; propagate runtime identity and reject mixed runs. |
| Cancellation handlers | Existing OperationCanceledException handlers continue to work for both APIs; the subtype adds run context. |
| Previously escaping executor exceptions | Normalize into Failed with AgentExecutionFailed; caller cancellation remains an exception. |
| Empty streaming output | Now Failed (AgentExecutionEmptyMessage), consistent with ExecuteAsync. |
| RAG ConversationId | Keep the wire field, populated from runtime RunId. Reusing a query no longer reuses this identifier; it is not a provider session. |

Real Codex/Claude adapters, tool bridges, persistence, Studio and workflow changes
are outside this change.

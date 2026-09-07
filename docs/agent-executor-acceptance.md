# Shared executor acceptance and adapter guide

The shared contracts are implemented by the existing runtime, result builder and
model executor. Real Codex/Claude adapters, persistence, approval/resumption,
workflow changes and Studio monitoring are outside this feature.

## Acceptance evidence

Tests below live under `tests/Runiq.AI.Agents.Tests`; they use controlled executors,
clients and retrieval services. They do not require live providers, credentials or
installed Codex/Claude processes.

| Contract | Evidence |
| --- | --- |
| Deferred start, fresh enumeration, timestamps and independent contexts | `AgentRunContextTests`, `AgentRunLifecycleTests` |
| All runtime overloads dispatch through DI; duplicate/missing/unsupported kinds | `AgentExecutionContractTests.Registry_DispatchesEachRegisteredKind`, `Registry_RejectsDuplicateKinds`, `Registry_MissingSelectionAndImplementationProduceDistinctFailures` |
| Scoped lifetime and disposal | `Registry_PreservesScopedDependencies` |
| Text, explicit JSON, owned JSON, empty responses, legacy factories | `StructuredOutput_FactoriesCloneJson`, `JsonOutput_RequiresExplicitExecutorPayload`, `EmptyCompletion_ProducesExplicitFailure`, `ResultFactories_PreserveStandaloneCompatibility` |
| Deterministic batch/stream equivalence across separate runs | `IndependentRuns_ComparePayloadsWithoutEquatingRunIdentity` |
| Same-run terminal metadata and partial steps | `ResultMetadata_MatchesPublishedRunAndRetainsPartialSteps`, `StructuredCompletion_PreservesMetadataAndMatchesAggregate` |
| Concurrent tool correlation, publication order and foreign metadata | `ConcurrentRuns_IsolateToolsAndMatchTheirOwnTerminal`, `Publication_OverridesForeignMetadataWithoutMutatingSourceEvents` |
| One terminal, missing terminal and no reads after terminal | `CustomExecutor_InvalidExecutionEndsWithOneFailure`, `TerminalPublication_DisposesWithoutAdvancingPastTerminal` |
| Pre-cancellation, mid-run cancellation, abandonment and cancellation classification | `AgentRunLifecycleTests`, `ModelExecutionBoundaryTests`, `AgentRuntimeDiagnosticsTests` |
| Existing model constructors, one tool loop and shared batch/stream execution | `ModelExecutionBoundaryTests.ModelLoop_PreservesSingleToolInvocation` |
| RAG, provider options, grounding, citation and readiness regressions | `AgentExecutionRuntimeTests`, existing provider tests and hosting RAG regressions |
| HTTP/SSE DTO serialization, metadata, failures and disconnected clients | `Hosting/Agents/AgentChatExecutionContractTests` |

Independent runtime calls are different runs: compare status, text/JSON and error
payloads, not RunId or timestamps. To reconstruct the result of one particular run,
aggregate that stream with `AgentExecutionResultBuilder` instead of executing again.

## Connecting an adapter

Implement the public `IAgentExecutor` with a stable `Kind` and one `ExecuteAsync`
event stream. Register the adapter in the host scope:

```csharp
services.AddSingleton(new Agent("assistant", "Assistant", "Help the user").UseCodex());
services.AddRuniqAgentServer();
services.AddScoped<IAgentExecutor, MyCodexExecutor>();

await using var scope = serviceProvider.CreateAsyncScope();
var runtime = scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>();
var result = await runtime.ExecuteAsync("assistant", new AgentQuery("Hello"), cancellationToken);
```

`MyCodexExecutor` denotes a future host-provided implementation, not a bundled
adapter. Add the SDK/client dependencies required by that implementation through
constructor injection. Keep scoped dependencies out of singleton services. The
default model executor remains registered alongside it. An unsupported kind does
not fall back to the model. Duplicate kind registrations fail at runtime resolution;
to replace the built-in model, explicitly remove its interface registration first.

Inside the adapter:

1. Read `request.Agent` and `request.Query` without mutating reusable definitions.
   Keep buffers, provider session state and resources local to each invocation.
2. Pass the supplied cancellation token to every external operation and check it
   before starting more work. RunId is correlation data, never a provider resume key.
3. Yield `AssistantDelta` for text and typed tool/RAG events where applicable.
   Preserve each tool call ID across its start and completion/failure events.
4. Yield `Completed(rag, citations, structuredOutput)` for success. JSON is optional
   and explicitly supplied; the factory clones it. Its presence does not imply
   schema validation. Text-free completion needs explicit JSON to succeed.
5. Yield `Failed` with a safe message and specific code for expected failures.
   Unexpected exceptions are normalized and logged by runtime. Caller cancellation
   propagates as `AgentRunCanceledException`; an unrelated OperationCanceledException
   is an execution failure when the caller token is not cancelled.
6. Release invocation resources in async iterator `finally` blocks or enumerator
   disposal. Runtime disposes before terminal publication and on abandonment. DI
   owns injected services. Do not retry side-effecting operations automatically.

Runtime assigns outer event identity, sequence and timestamps and owns terminal
transitions. It ignores events after the first terminal and reports a missing
terminal as `AgentExecutionProtocolError`. Completion does not acknowledge delivery
to a disconnected client. No arbitrary provider-payload envelope is available.

See [the lifecycle and compatibility contract](agent-execution-lifecycle.md) for
cancellation, standalone factories, HTTP/SSE field names and JSON null semantics.

## Local delivery verification

Run the repository CI checks locally without its remote publication steps:

```powershell
dotnet restore Runiq.AI.slnx
./scripts/test-update-test-badge.ps1
dotnet build Runiq.AI.slnx --no-restore -c Release -v:minimal
dotnet test Runiq.AI.slnx --no-build -c Release -v:minimal --logger trx --results-directory TestResults
dotnet pack Runiq.AI.slnx --no-build -c Release -o artifacts/packages
git diff --check
```

Report observed results, including warnings and skipped checks; test counts are
measurements, not acceptance targets. New public APIs require English XML comments;
each new test method requires an immediately preceding English explanatory comment.
Commit, push, release and deployment require explicit user authorization.

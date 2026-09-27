# Programmatic Memory conversation

Run `dotnet run --project samples/Runiq.AI.MemoryConversation` from the repository root.
Expected output includes a thread ID, `Hello, Ada.`, then `Your name is Ada.`.

This console uses the real runtime with a deterministic Core chat client, so no API key,
network model call, dashboard, or HTTP endpoint is required. It explicitly registers Memory,
its volatile in-memory provider, trusted host identity/access adapters, and a model agent
with `.UseMemory()`. `Services/ConversationDemo.cs` makes the first aggregated call and
continues the returned `ThreadId` through the streaming API. Both use the same lifecycle.

The fixed identity belongs only to this local console. Production hosts must resolve verified
identity and real resource membership; user-supplied thread/turn IDs are never capabilities.
In-memory data lasts only for this DI container. For persistence across restarts, explicitly
select `AddRuniqMemoryPostgreSql` instead and apply the provider's migrations; see
[the persistence guide](../../docs/memory-persistence.md).

Retain the thread ID and logical `MemoryTurnId` to detect duplicate requests. Reusing the
same pair is rejected; it does not resume a model invocation. Each invocation has a fresh
`RunId`. See [multi-turn semantics](../../docs/memory-conversations.md) for partial output,
concurrency, retry, cancellation, and interrupted-process behavior.

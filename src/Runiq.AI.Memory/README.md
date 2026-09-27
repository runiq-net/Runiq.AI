# Runiq.AI.Memory

Memory foundations: identity, opt-in configuration, and ownership authorization.
This package does not yet persist or replay conversations (#200/#201).

Dependency direction: `Agents -> Memory -> Core`. Memory has no Agents, HTTP request,
RAG, or SQL contracts. Existing Core hosting dependencies remain unchanged.
The in-memory provider belongs here in #200; PostgreSQL and migrations belong in
the future, explicitly installed `Memory.PostgreSql -> Memory` package.
The optional `Memory.Rag -> Memory + Rag` adapter belongs to #207.

`ThreadId` identifies a conversation, `ResourceId` its user/project/domain owner,
`RunId` one Agents invocation, and `ProviderSessionId` native executor continuation.
Neither invocation nor provider session identifiers grant Memory access.
Identifiers contain 1-256 characters, no control characters or surrounding whitespace.
Comparison is ordinal and case-sensitive, except agent IDs follow the existing
case-insensitive agent registry (canonical uppercase). Scope components remain separate.

Thread ownership binds a conversation to a tenant/application, resource, and agent.
Sharing must be explicit and cannot cross a tenant boundary. Resource ownership is
distinct from caller identity. Host adapters supply authoritative metadata; a missing
existing thread fails closed. Future stores must atomically bind new ownership and
recheck it on every operation so stale decisions cannot rebind a conversation.

## Registration and authorization

Referencing Memory (including transitively through Agents) enables nothing. Register
the neutral foundation and host adapters separately from an agent's `UseMemory()`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Runiq.AI.Memory.Abstractions;
using Runiq.AI.Memory.DependencyInjection;

// Host implementations: no framework store or identity is registered implicitly.
services.AddRuniqMemory();
services.AddScoped<IMemoryIdentityResolver, HostIdentityResolver>();
services.AddScoped<IMemoryOwnershipLookup, HostOwnershipLookup>();
services.AddScoped<IMemoryAccessPolicy, HostResourcePolicy>();
```

The three `Host*` types above are application implementations, not shipped providers.
`IMemoryIdentityResolver.ResolveAsync` reads verified host state each run;
`IMemoryAccessPolicy.CanAccessResourceAsync` authorizes the caller's domain membership;
`IMemoryOwnershipLookup.FindAsync` reads authoritative metadata without transcript content.
Register all three as scoped (singleton adapters are appropriate only when stateless and
safe for concurrent callers). `AddRuniqMemory` registers scoped authorization, is
idempotent, and does no network, database, model, or migration work.

For standalone use, resolve `MemoryAuthorizationService` inside a DI scope and call:

```csharp
using Runiq.AI.Memory.Configuration;
using Runiq.AI.Memory.Models;

var identity = await identityResolver.ResolveAsync(cancellationToken);
if (identity is null) throw new UnauthorizedAccessException();
var context = await authorization.AuthorizeAsync(identity,
    new MemoryReference("project-42", "existing-thread"),
    "support-agent", new MemoryOptions(), cancellationToken);
if (context is null) throw new UnauthorizedAccessException();
```

Missing/incorrect ownership, denied membership, and incompatible sharing all return
null. Adapter exceptions propagate to standalone callers, who must map them safely;
Agents maps them to safe runtime failures. Cancellation propagates to each adapter.
No message content is read by this operation.

## Scopes and explicit sharing

`new MemoryOptions()` selects thread scope. `new MemoryOptions(MemoryScope.Resource)`
declares future recall across threads in the same authorized resource and agent scope;
it does not enable resource-wide recall in this foundation. Both require an explicit
`MemoryReference`. A null `ThreadId` requests a new conversation; an existing ID must
already have authoritative metadata. Never treat an unknown ID as a create request.

`new MemoryOptions(MemoryScope.Resource, sharingGroup: "support-team")` opts into a
host-managed group. Both stored ownership and requesting configuration must name the
same group, and `IMemoryAccessPolicy.CanShareAsync` must authorize both agents and
the caller. Its default implementation denies sharing. This check also runs when
proposing a new shared thread. Group names alone grant no membership. Removing or
changing a stored thread's group is not an implicit ownership migration.

The result separates `Ownership.Scope` (the original binding) from `AccessScope`
(the requesting agent). `IsNewThread` identifies an unpersisted, generated proposal.
Do not pass `MemoryContext` to clients and later trust it as an authorization token.
Hosts must not construct verified identity from arbitrary request data.

## Delivery boundaries and validation

Source files and namespaces follow the existing RAG package's responsibility layout:

| Directory / namespace suffix | Types |
| --- | --- |
| `Models` | `MemoryAccessScope`, `MemoryContext`, `MemoryIdentity`, `MemoryReference`, `MemoryThreadOwnership` |
| `Abstractions` | `IMemoryAccessPolicy`, `IMemoryIdentityResolver`, `IMemoryOwnershipLookup` |
| `Configuration` | `MemoryOptions`, `MemoryScope` (in MemoryOptions.cs) |
| `Services` | `MemoryAuthorizationService` |
| `DependencyInjection` | `MemoryServiceCollectionExtensions` |
| `Validation` | Internal `MemoryIdentifier` |

The pre-release #199 source layout correction moves these types from the flat
`Runiq.AI.Memory` namespace to the matching suffixes above. Consumers of the previous
working-tree version need the corresponding `using` directives (or qualified names);
type/member names, signatures apart from their namespace qualification, and behavior
are unchanged. No duplicate compatibility types or registration layer is introduced.

See [Agents integration](../Runiq.AI.Agents/README.md#memory-foundations) for HTTP,
background-host examples and executor support. See the repository's
[Memory delivery map](../../docs/memory-foundations.md) for PBI ownership and evidence.

`Runiq.AI.Memory.Tests` references Memory without Agents and tests identifiers,
configuration, policies, cancellation, and project dependency boundaries. Agent runtime
and HTTP behavior belong in `Runiq.AI.Agents.Tests`. Future providers own separate
test projects. #199 supplies no PostgreSQL migrations, conversation CRUD, transcript
storage/replay, context budgeting, vector recall, or Dashboard conversation navigation.

# Memory persistence (#200)

#218–#224 deliver conversation/message storage. Dependency order is #218 → #219;
#218 → #220 → #221; #219/#221 → #222 → #223 → #224. Agent model history replay and
runtime persistence orchestration are documented in [#201 conversations](memory-conversations.md). Retention and deletion UI remain
in #204. Merely referencing packages or selecting a provider does not enable agents.

## Packages and ownership

`Agents -> Memory -> Core`; `Memory.PostgreSql -> Memory`. Core does not reference
Memory. Base Memory has no RAG or PostgreSQL dependency. Persistence reuses Core
`ChatMessage`, `ChatRole`, and `ChatToolCall`; stored models contain no Agent,
executor, HTTP DTO, or provider session objects.

The host supplies scoped `IMemoryIdentityResolver` and `IMemoryAccessPolicy` adapters.
Resolve identity from authenticated host state, never request-body identity fields.
The explicitly selected provider supplies both `IMemoryConversationStore` and
`IMemoryOwnershipLookup` from the same scoped instance. `AddRuniqMemory()` registers
only neutral authorization and never chooses a store. Missing provider selection
fails during service resolution. Repeated selection is safe: the last provider wins
for both interfaces. The host's identity/policy registrations are preserved.

`AddRuniqMemoryInMemory()` selects volatile storage. Its data-only singleton lives
for the service-provider lifetime and survives request scopes, but a new independent
host starts empty. `AddRuniqMemoryPostgreSql(...)` selects durable SQL storage. Its
Memory-owned singleton connection pool is disposed by the container; scoped stores
do not capture a scoped caller or policy in that singleton. Registration and service
resolution open no connections and run no migrations.

## Local PostgreSQL and migrations

Reuse the existing database; do not delete the persistent volume to start or restart:

```powershell
docker compose -f docker-compose.rag-postgresql.yml up -d
$env:RUNIQ_MEMORY_CONNECTION = 'Host=localhost;Port=54329;Database=runiq_rag_dev;Username=runiq_dev;Password=runiq_dev_only'
```

These credentials belong only to the repository's local development Compose service.
Supply deployment credentials through host configuration/secret management. A schema
must be a dedicated lowercase ASCII identifier of 1–63 characters, start with a letter
or underscore, and not be a system/public schema. Identifiers are safely quoted.

The provider defaults to `runiq_memory`. It owns `conversations`, `messages`,
`append_receipts`, and `migration_history`, independent of RAG's document tables and
`schema_migrations`. No pgvector extension is needed by Memory. Sharing a database
or connection string with RAG does not share pools/options/history: Memory does not
replace the unkeyed `NpgsqlDataSource` registered by RAG.

Explicitly resolve `PostgreSqlMemoryMigrator` and call `MigrateAsync`. No implicit
startup flag is used. Embedded SQL runs inside one transaction, including schema,
tables, and history recording. A database advisory transaction lock serializes
independent hosts. History versions must be contiguous and their checksums must match;
newer/modified histories fail with `IncompatibleSchema`, with no automatic downgrade.
Failures roll back, so retrying migration is safe. SQL schema v1 is the initial release;
test-only migration fixtures exercise upgrades and rollback without shipping a fake v2.

For production, run this explicit migration call as a controlled deployment step with
a migration identity allowed to create schemas/tables/functions. Application identities
need only schema usage, table SELECT/INSERT and conversation version UPDATE; they must
not edit ownership, receipts, or migration history. Start application hosts after the
migration succeeds. Back up normally before future schema upgrades. No deployment or
package publication is part of this change.

## Compilable consumer example

The following console example references `Runiq.AI.Memory.PostgreSql`. It uses an
explicit trusted local background identity and a narrowly scoped demonstration policy;
replace those two host adapters with application authentication/membership adapters.
For an in-memory host, replace `AddRuniqMemoryPostgreSql` with `AddRuniqMemoryInMemory`
and omit the PostgreSQL migrator call and provider namespace imports.

```csharp
using Microsoft.Extensions.DependencyInjection;
using Runiq.AI.Core.AI.Chat;
using Runiq.AI.Memory.Abstractions;
using Runiq.AI.Memory.Configuration;
using Runiq.AI.Memory.DependencyInjection;
using Runiq.AI.Memory.Models;
using Runiq.AI.Memory.PostgreSql.DependencyInjection;
using Runiq.AI.Memory.PostgreSql.Migrations;
using Runiq.AI.Memory.Services;

var services = new ServiceCollection();
services.AddRuniqMemory();
services.AddScoped<IMemoryIdentityResolver, LocalIdentity>();
services.AddScoped<IMemoryAccessPolicy, LocalResourcePolicy>();
services.AddRuniqMemoryPostgreSql(options =>
{
    options.ConnectionString = Environment.GetEnvironmentVariable("RUNIQ_MEMORY_CONNECTION")
        ?? throw new InvalidOperationException("Set RUNIQ_MEMORY_CONNECTION first.");
    options.Schema = Environment.GetEnvironmentVariable("RUNIQ_MEMORY_SCHEMA") ?? "runiq_memory";
});
await using var host = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
await host.GetRequiredService<PostgreSqlMemoryMigrator>().MigrateAsync();
await using var scope = host.CreateAsyncScope();
var identity = await scope.ServiceProvider.GetRequiredService<IMemoryIdentityResolver>().ResolveAsync(default)
    ?? throw new UnauthorizedAccessException();
var authorization = scope.ServiceProvider.GetRequiredService<MemoryAuthorizationService>();
var context = await authorization.AuthorizeAsync(identity, new MemoryReference("project-42"),
    "support-agent", new MemoryOptions(MemoryScope.Resource)) ?? throw new UnauthorizedAccessException();
var store = scope.ServiceProvider.GetRequiredService<IMemoryConversationStore>();
var conversation = await store.CreateAsync(context);
// Retain the same request object (or exactly the same fields) across uncertain-response retries.
var request = new MemoryAppendRequest(Guid.NewGuid().ToString("N"), conversation.Version,
    [new MemoryMessage(Guid.NewGuid().ToString("N"), "run-42", DateTimeOffset.UtcNow,
        new ChatMessage(ChatRole.User, "Where is my order?"))]);
var receipt = await store.AppendAsync(context, request);
var retry = await store.AppendAsync(context, request);
if (retry != receipt) throw new InvalidOperationException("Retry receipt changed.");
var history = await store.ReadMessagesAsync(context);
var page = await store.ListAsync(context, limit: 20);
Console.WriteLine($"Thread {conversation.Ownership.ThreadId}: {history.Count} message, version {receipt.Version}.");

sealed class LocalIdentity : IMemoryIdentityResolver
{
    public ValueTask<MemoryIdentity?> ResolveAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<MemoryIdentity?>(new("local-worker", "local-app"));
    }
}
sealed class LocalResourcePolicy : IMemoryAccessPolicy
{
    public ValueTask<bool> CanAccessResourceAsync(MemoryIdentity identity, string resourceId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(identity.CallerId == "local-worker" &&
            identity.BoundaryId == "local-app" && resourceId == "project-42");
    }
}
```

To continue an existing thread after restart, authorize with
`new MemoryReference("project-42", savedThreadId)`. Missing metadata fails closed;
an unknown supplied ThreadId never authorizes creation. A null ThreadId requests a
fresh generated proposal. Calling `CreateAsync` atomically binds tenant, thread,
resource, normalized agent and optional sharing group; identical binding retries
return the existing metadata and conflicting rebinding raises `OwnershipConflict`.

## Access, ordering, retry and cancellation contract

Every content operation rechecks authoritative ownership and host policy.
`MemoryContext` is not a persistent access capability. Tenant/resource/thread IDs
compare ordinally; agent IDs keep #199 uppercase normalization. Sharing requires
equal explicit sharing groups and host approval for both requesting and owning agents.
Knowing a thread ID alone provides no access. Metadata lookup exposes no transcript.

Thread scope lists only its own thread. Resource scope lists matching conversations
in the same tenant/resource/agent or permitted sharing scope. Listing requires an
existing authorized anchor conversation. List pages sort ascending by ordinal ThreadId
(including supplementary Unicode characters), use the last returned ID as an exclusive
cursor, and apply policy before counting page items. Message pages sort by sequence and
use the last returned sequence as an exclusive cursor, initially zero. Limits are 1–1000;
empty pages end traversal. There is no total-count or snapshot-pagination promise:
new threads preceding the cursor require a fresh traversal, and subsequent message
pages may see later committed appends.

New conversations have version 0. Sequences begin at 1. An append commits a nonempty,
ordered batch, advances version to its last assigned sequence, and stores a receipt
atomically. Store-assigned sequence, not message wall-clock time, determines order.

After access validation, decisions have this order:

1. Existing tenant/thread/key plus identical logical payload returns the original
   sequence-range receipt, even if later writes have advanced the conversation.
2. Existing key plus a changed payload raises `IdempotencyConflict` without mutation.
3. A new key with a stale `ExpectedVersion` raises `VersionConflict`.
4. Reusing a message ID under a new key at the current version raises `MessageConflict`.
5. Invalid tool links raise `ToolRelationshipConflict`; otherwise the whole batch commits.

Logical equality includes ExpectedVersion, batch order/count, every message ID, run ID,
timestamp (including original offset and ticks), role, exact content, tool result ID,
and ordered call IDs/names/exact argument strings. JSON inside arguments is text:
`{}` and `{ }` are different. Caller identity and proposed context timestamps are not
part of the payload; access is independently rechecked before returning any receipt.
Identifiers and message text must be well-formed Unicode; malformed surrogate sequences
are rejected before JSON/database encoding can silently replace them. Empty/null batches,
duplicate IDs within a batch, negative versions, invalid identifiers and invalid message
shapes fail during request construction with argument-validation exceptions.
Request keys are tenant/thread-scoped, not globally scoped. Keep the original request
key **and all original fields** across retries; do not regenerate timestamps or IDs.

Concurrent distinct requests at one expected version have at most one winner. After a
`VersionConflict`, reread the conversation and decide whether to submit a new logical
request with a new key and current version. After an uncertain commit response, retry
the **original** request first. Do not silently retry it as a new key/version. PostgreSQL
uses a conversation row lock and transactional constraints across hosts; in-memory
uses per-conversation gates, with async host policy calls outside those gates.

Pre-cancelled operations perform no mutation. Cancellation is passed through policy,
connection, command, lock wait, and commit operations. A cancellation/failure before
commit leaves no partial batch or consumed key. Once commit may have occurred, the
caller cannot infer rollback from a lost response: retry the same request safely.
Expected conflicts are `MemoryStoreException` categories. Database failures use
`StorageFailure` with an inner diagnostic; never expose credentials/content from inner
exceptions to clients. There is no fallback to in-memory on PostgreSQL failure.

## Tool relationships and serialization

Assistant calls have unique `(run ID, call ID)` pairs within a conversation. Results
must use Core Tool role and reference a unique earlier call in the same conversation
and run, either from an earlier append or earlier message in the batch. A call may
remain pending. Duplicate calls/results, results before calls, unresolved results,
and cross-run results are rejected atomically. Only assistant messages contain calls;
only tool results contain `ToolCallId`.

`MemoryMessageSerializer` defines strict JSON **payload v1**, separate from SQL schema
v1. All original timestamp ticks/offsets and Core message fields survive round trips.
The initial release supports only v1; there is no historical v0 format to invent.
A fixed v1 fixture verifies compatibility, and unsupported versions, missing required
fields, unknown/duplicate fields, malformed JSON or invalid model values fail as `InvalidPayload`.
Future schema migrations must preserve readable payloads and existing request receipts;
future serializers need an explicit compatibility reader before changing the format.

No deletion/retention orchestration is introduced here. #204 must account for messages,
ownership and **idempotency receipts together**, including how retry behavior changes
after deletion. No conversation UI, model replay, embeddings, budget policy, or derived
memory features are included in #200.

## Organization and acceptance evidence

Memory uses `Models`, `Abstractions`, `Services`, `Validation`, `Serialization`,
`Persistence/InMemory`, and `DependencyInjection`. PostgreSQL uses `Configuration`,
`Persistence`, `Migrations`, `DependencyInjection`, and `Properties`. Namespaces match
these responsibilities. The shared provider scenarios are linked **test source** under
`tests/Shared/Memory`; Memory.Tests references no SQL provider. The test-only executable
under `tests/Hosts` supplies real independent processes, without adding a sample product.

| Parent #200 criterion | Implementation and behavior evidence |
| --- | --- |
| Create/list/read conversations and append ordered messages | IMemoryConversationStore; shared CreateAppendRead and ListingAndSharing scenarios |
| Preserve user/assistant/tool identity, run, timestamps/order | MemoryMessage/StoredMemoryMessage; shared metadata scenario and process restart test |
| Preserve tool-call/result relationships | MessageValidation; shared ToolRelationships and InvalidBatch scenarios |
| History survives host restart | PostgreSqlHostLifecycleTests.ProcessRestart_PreservesHistoryAndOriginalRetry; fully exited/restarted processes |
| Repeated writes do not duplicate messages | Shared RetriesAndConflicts and ConcurrentWrites; durable host retry |
| Concurrent ordering and documented conflicts | ExpectedVersion and receipts; IndependentProcesses_HaveOneVersionWinner; this guide |
| Isolated SQL dependencies, migrations, real integration tests | Provider-only Npgsql; PostgreSqlMigrationTests and PostgreSqlFailureTests |
| New packable provider/test projects and correct references | csproj/solution; MemoryContractTests project boundary checks; local pack |
| Reuse #210, atomic ownership binding, reject rebinding, scoped access | Existing context/ownership/scope/policy contracts; shared CompetingBindings, IsolationAndRevocation and SharingPolicy scenarios |
| Consistent idempotency/version semantics across hosts/providers | Same linked scenario code in both suites; independent host process race |
| Neutral persisted models and Core primitives | Models contain Core ChatMessage and string IDs; no Agents/executor/HTTP types |
| Memory-owned schema/history separate from RAG | Embedded SQL and migrator; SharedDatabase_PreservesRagDocumentsAndMigrationHistory |
| Explicit host selection, volatile in-memory, no implicit SQL I/O | Registration tests including unreachable database; IndependentHost_StartsEmpty; Agents disabled-Memory regression |
| Shared real-database scenarios for isolation/binding/duplicates/concurrency/restart | tests/Shared/Memory linked by both suites; PostgreSqlHostLifecycleTests |
| Explicit serialization evolution and local migration guide; defer retention | MemorySerializationTests, corruption tests, migration fixtures; this guide and #204 handoff |

## Validation commands

```powershell
dotnet test tests/Runiq.AI.Memory.Tests
dotnet test tests/Runiq.AI.Memory.PostgreSql.Tests
dotnet test tests/Runiq.AI.Agents.Tests --filter 'FullyQualifiedName~Memory'
dotnet build Runiq.AI.slnx -c Release
dotnet test Runiq.AI.slnx -c Release --no-build
dotnet pack Runiq.AI.slnx -c Release --no-build -o "$env:TEMP/runiq-issue200-packages"
```

PostgreSQL tests use the existing Compose connection by default, or
`RUNIQ_MEMORY_TEST_CONNECTION`. Database unavailability **fails** selected tests; it
never silently skips/passes. Fixtures own unique `memory_test_*` schemas and clean up
only those schemas. Host restart tests preserve the database/volume and do not restart
the shared RAG container. No paid models are invoked.

### Actual local validation — 2026-09-27

| Check | Actual result |
| --- | --- |
| Release solution build | Passed, 0 errors; 18 existing SourceLink dependency NU1902 warnings |
| Release solution tests, final `-m:1` run | 1,674 passed, 0 failed, 5 existing opt-in live CLI tests skipped across 9 test projects |
| Memory.Tests | 46 passed, including 12 shared provider scenarios; no database dependency |
| Memory.PostgreSql.Tests | 30 passed against real PostgreSQL, no skipped integration tests |
| Agents focused Memory regressions | 66 passed |
| Agents full suite, final run | 751 passed, 5 existing opt-in live CLI tests skipped |
| RAG PostgreSQL regression suite | 29 passed against the same Compose database |
| Local solution pack | Passed; Memory depends only on Core, and Memory.PostgreSql depends only on Memory and Npgsql |
| Consumer example above | Extracted verbatim, compiled in Release, and executed against a unique PostgreSQL schema; create/append/retry/read succeeded |
| Whitespace and dependency-boundary review | Passed; new tests have explanatory English comments and new public APIs have XML documentation |
| Fixture cleanup | No `memory_test_*` schemas remain; shared database and volume retained |

The initial parallel solution test run had one existing CLI bridge timing failure:
`CodexToolBridgeTests.Runtime_CancelsActiveToolAndClosesBridge(timeout: true)` timed out
waiting for its tool-start probe. Both targeted variants passed on rerun, followed by
the complete passing solution run with `dotnet test Runiq.AI.slnx -c Release --no-build -m:1`.
No unrelated CLI production/test code was changed to conceal that result.

Existing SourceLink `Microsoft.Build.Tasks.Git 8.0.0` NU1902 warnings remain. Local pack
also reports the existing RAG prerelease PdfPig dependency NU5104 warning and a
non-packable sample warning. These unrelated dependencies were not upgraded.

Build/test logs, TRX reports, compiled consumer and local packages are under
`%TEMP%/runiq-issue200-validation`, outside the review tree. The initial failed run and
the final successful run are both retained. Nothing was committed, pushed, published,
deployed or changed on GitHub; issue completion here describes the local implementation.


## Turn lifecycle (#201 / #226)

Migration `002_turns.sql` adds neutral turn state without rewriting message JSON v1 or
existing append receipts. `MemoryAppendRequest.Turn` commits with its ordered message
batch and receipt. A new running turn requires exactly one user message at the expected
conversation version. Only its owning invocation may append while it is running.
Assistant tool calls precede results; completion rejects unresolved calls. Terminal writes
may be empty: their receipt has `FirstSequence = Version + 1`, with no new message sequence.
The conversation version remains the last message sequence, as in #200.

An interrupted process leaves a durable `Running` turn. Failed/cancelled output remains
associated with that outcome; it is never evidence of a completed answer. No automatic
lease expiry or tool re-execution is provided. All turn reads and writes revalidate existing
ownership and access policy. Existing messages lacking turn state retain their storage
representation and retry receipts; their outcome is unknown and they are excluded from
runtime replay. Retention/deletion in #204 must include turn state and append receipts.

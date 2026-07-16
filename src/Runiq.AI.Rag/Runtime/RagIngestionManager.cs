using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Runiq.AI.Rag.Abstractions.Services;
using Runiq.AI.Rag.Configuration;

namespace Runiq.AI.Rag.Runtime;

internal sealed class RagIngestionManager(IRagIndexRegistry registry, IServiceScopeFactory scopeFactory, ILogger<RagIngestionManager>? logger = null) : IRagIngestionManager
{
    private readonly object gate = new();
    private readonly ILogger<RagIngestionManager> log = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<RagIngestionManager>.Instance;
    private readonly Dictionary<string, Entry> entries = registry.Registrations.ToDictionary(r => r.Name, _ => new Entry(), StringComparer.Ordinal);

    public Task<RagIngestionOperation> StartAsync(string indexName, CancellationToken cancellationToken = default) => StartAsync(indexName, RagIngestionOperationReason.Manual, cancellationToken);

    internal Task<RagIngestionOperation> StartAsync(string indexName, RagIngestionOperationReason reason, CancellationToken cancellationToken = default)
    {
        var registration = registry.Registrations.SingleOrDefault(r => string.Equals(r.Name, indexName, StringComparison.Ordinal))
            ?? throw new KeyNotFoundException($"RAG index '{indexName}' is not registered.");
        Entry entry;
        lock (gate)
        {
            entry = entries[indexName];
            if (entry.ActiveTask is { IsCompleted: false }) throw new InvalidOperationException($"RAG index '{indexName}' already has an active ingestion operation.");
            entry.Cancellation?.Dispose();
            entry.Cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            entry.Operation = NewOperation(indexName, reason);
            entry.ActiveTask = RunAsync(registration, entry);
            return entry.ActiveTask;
        }
    }

    public RagIndexRuntimeStatus GetStatus(string indexName)
    {
        lock (gate)
        {
            if (!entries.TryGetValue(indexName, out var entry)) throw new KeyNotFoundException($"RAG index '{indexName}' is not registered.");
            var active = entry.ActiveTask is { IsCompleted: false } ? entry.Operation : null;
            return new RagIndexRuntimeStatus { IndexName = indexName, Readiness = GetReadiness(entry, active), ActiveOperation = active, LastOperation = entry.LastOperation };
        }
    }

    public async Task CancelAsync(string indexName, CancellationToken cancellationToken = default)
    {
        Task<RagIngestionOperation>? task;
        lock (gate)
        {
            if (!entries.TryGetValue(indexName, out var entry)) throw new KeyNotFoundException($"RAG index '{indexName}' is not registered.");
            task = entry.ActiveTask;
            if (task is null || task.IsCompleted) return;
            entry.Operation = entry.Operation! with { State = RagIngestionOperationState.Cancelling };
            entry.Cancellation!.Cancel();
        }
        await task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<RagIngestionOperation> RunAsync(RagIndexRegistration registration, Entry entry)
    {
        var token = entry.Cancellation!.Token;
        log.LogInformation("RAG ingestion {OperationId} started for index {IndexName} with reason {Reason}.", entry.Operation!.OperationId, registration.Name, entry.Operation.Reason);
        try
        {
            using var scope = scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IRagService>();
            foreach (var source in registration.Sources)
            {
                token.ThrowIfCancellationRequested();
                Update(entry, operation => operation with { Progress = operation.Progress with { CurrentSource = source.Identity } });
                log.LogInformation("RAG ingestion {OperationId} started discovery for source {SourceIdentity} in index {IndexName}.", entry.Operation!.OperationId, source.Identity, registration.Name);
                var report = await service.IngestAsync(source, registration.Name, token).ConfigureAwait(false);
                Update(entry, operation => operation with { Progress = Add(operation.Progress, report, source.Identity) });
                log.LogInformation("RAG ingestion {OperationId} completed source {SourceIdentity} with {DiscoveredDocuments} discovered documents.", entry.Operation!.OperationId, source.Identity, report.DiscoveredDocuments);
            }
            var terminal = entry.Operation!.Progress.FailedDocuments > 0 ? RagIngestionOperationState.PartiallyCompleted : RagIngestionOperationState.Completed;
            return Complete(entry, terminal);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            log.LogInformation("RAG ingestion {OperationId} was cancelled for index {IndexName}.", entry.Operation!.OperationId, registration.Name);
            return Complete(entry, RagIngestionOperationState.Cancelled);
        }
        catch (Exception exception)
        {
            log.LogError(exception, "RAG ingestion {OperationId} failed for index {IndexName}.", entry.Operation!.OperationId, registration.Name);
            var failure = new RagIngestionRuntimeFailure { Code = "IngestionFailed", Message = "The ingestion operation failed.", SourceIdentity = entry.Operation.Progress.CurrentSource, Timestamp = DateTimeOffset.UtcNow };
            Update(entry, operation => operation with { Progress = operation.Progress with { LastFailure = failure } });
            return Complete(entry, RagIngestionOperationState.Failed);
        }
    }

    private RagIngestionOperation Complete(Entry entry, RagIngestionOperationState state)
    {
        lock (gate)
        {
            var completedAt = DateTimeOffset.UtcNow;
            entry.Operation = entry.Operation! with { State = state, CompletedAt = completedAt, Duration = completedAt - entry.Operation.StartedAt, Progress = entry.Operation.Progress with { CurrentSource = null, CurrentDocument = null } };
            entry.LastOperation = entry.Operation;
            if (state is RagIngestionOperationState.Completed or RagIngestionOperationState.PartiallyCompleted) entry.HasUsableIndex = true;
            return entry.Operation;
        }
    }

    private void Update(Entry entry, Func<RagIngestionOperation, RagIngestionOperation> update) { lock (gate) entry.Operation = update(entry.Operation!); }
    private static RagIngestionOperation NewOperation(string name, RagIngestionOperationReason reason) => new() { OperationId = Guid.NewGuid(), IndexName = name, Reason = reason, State = RagIngestionOperationState.Running, StartedAt = DateTimeOffset.UtcNow, Progress = new() };
    private static RagIngestionProgress Add(RagIngestionProgress p, Models.Ingestion.RagIngestionReport r, string source) => p with { DiscoveredDocuments = p.DiscoveredDocuments + r.DiscoveredDocuments, ProcessedDocuments = p.ProcessedDocuments + r.CreatedDocuments + r.UpdatedDocuments + r.SkippedDocuments + r.FailedDocuments, AddedDocuments = p.AddedDocuments + r.CreatedDocuments, UpdatedDocuments = p.UpdatedDocuments + r.UpdatedDocuments, SkippedDocuments = p.SkippedDocuments + r.SkippedDocuments, DeletedDocuments = p.DeletedDocuments + r.DeletedDocuments, FailedDocuments = p.FailedDocuments + r.FailedDocuments, ProducedChunks = p.ProducedChunks + r.CreatedChunks, ProducedEmbeddings = p.ProducedEmbeddings + r.CreatedChunks, CurrentSource = source, LastFailure = r.Failures.LastOrDefault() is { } f ? new() { Code = "DocumentFailed", Message = "A document could not be ingested.", SourceIdentity = source, DocumentIdentity = f.DocumentId, Timestamp = DateTimeOffset.UtcNow } : p.LastFailure };
    private static RagIndexReadiness GetReadiness(Entry e, RagIngestionOperation? active) => active is not null ? e.HasUsableIndex ? RagIndexReadiness.Ready : RagIndexReadiness.Initializing : e.LastOperation?.State switch { RagIngestionOperationState.Completed => RagIndexReadiness.Ready, RagIngestionOperationState.PartiallyCompleted => RagIndexReadiness.Degraded, RagIngestionOperationState.Failed when e.HasUsableIndex => RagIndexReadiness.Degraded, RagIngestionOperationState.Failed => RagIndexReadiness.Failed, _ when e.HasUsableIndex => RagIndexReadiness.Ready, _ => RagIndexReadiness.NotInitialized };
    private sealed class Entry { public CancellationTokenSource? Cancellation; public Task<RagIngestionOperation>? ActiveTask; public RagIngestionOperation? Operation; public RagIngestionOperation? LastOperation; public bool HasUsableIndex; }
}

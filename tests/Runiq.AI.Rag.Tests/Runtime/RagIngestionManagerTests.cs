using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Runiq.AI.Rag.Abstractions.Ingestion;
using Runiq.AI.Rag.Abstractions.Services;
using Runiq.AI.Rag.Configuration;
using Runiq.AI.Rag.DependencyInjection;
using Runiq.AI.Rag.Models.Context;
using Runiq.AI.Rag.Models.Ingestion;
using Runiq.AI.Rag.Models.Queries;
using Runiq.AI.Rag.Runtime;

namespace Runiq.AI.Rag.Tests.Runtime;

public sealed class RagIngestionManagerTests
{
    // Verifies that a registered manual index remains uninitialized until explicitly started.
    [Fact]
    public void ManualIndex_ShouldNotStartDuringRegistration()
    {
        using var provider = CreateProvider(new ControlledRagService());
        var manager = provider.GetRequiredService<IRagIngestionManager>();

        var status = manager.GetStatus("documents");

        Assert.Equal(RagIndexReadiness.NotInitialized, status.Readiness);
        Assert.Null(status.ActiveOperation);
    }

    // Verifies that explicit ingestion stores a completed operation and monotonic report counters.
    [Fact]
    public async Task StartAsync_ShouldStoreCompletedProgress()
    {
        using var provider = CreateProvider(new ControlledRagService());
        var manager = provider.GetRequiredService<IRagIngestionManager>();

        var operation = await manager.StartAsync("documents");
        var status = manager.GetStatus("documents");

        Assert.Equal(RagIngestionOperationState.Completed, operation.State);
        Assert.Equal(RagIngestionOperationReason.Manual, operation.Reason);
        Assert.Equal(2, operation.Progress.DiscoveredDocuments);
        Assert.Equal(2, operation.Progress.ProcessedDocuments);
        Assert.Equal(RagIndexReadiness.Ready, status.Readiness);
        Assert.Equal(operation.OperationId, status.LastOperation!.OperationId);
    }

    // Verifies that unknown index names fail before any ingestion service is invoked.
    [Fact]
    public async Task StartAsync_ShouldRejectUnknownIndex()
    {
        using var provider = CreateProvider(new ControlledRagService());
        await Assert.ThrowsAsync<KeyNotFoundException>(() => provider.GetRequiredService<IRagIngestionManager>().StartAsync("missing"));
    }

    // Verifies that the same index cannot run two operations concurrently.
    [Fact]
    public async Task StartAsync_ShouldRejectConcurrentOperationForSameIndex()
    {
        var service = new ControlledRagService(block: true);
        using var provider = CreateProvider(service);
        var manager = provider.GetRequiredService<IRagIngestionManager>();
        var first = manager.StartAsync("documents");
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.StartAsync("documents"));
        service.Release.TrySetResult();
        await first;
    }

    // Verifies that explicit cancellation transitions an active operation to Cancelled instead of Failed.
    [Fact]
    public async Task CancelAsync_ShouldProduceCancelledOperation()
    {
        var service = new ControlledRagService(block: true);
        using var provider = CreateProvider(service);
        var manager = provider.GetRequiredService<IRagIngestionManager>();
        var operation = manager.StartAsync("documents");
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await manager.CancelAsync("documents");

        Assert.Equal(RagIngestionOperationState.Cancelled, (await operation).State);
        Assert.Equal(RagIndexReadiness.NotInitialized, manager.GetStatus("documents").Readiness);
    }

    // Verifies cancellation wins deterministically once the running pipeline observes its cancellation token.
    [Fact]
    public async Task CancelAsync_ShouldPreserveCancelledState_WhenPipelineIsReleasedAfterCancellation()
    {
        var service = new ControlledRagService(block: true, holdCancellation: true);
        using var provider = CreateProvider(service);
        var manager = provider.GetRequiredService<IRagIngestionManager>();
        var operation = manager.StartAsync("documents");
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var cancellation = manager.CancelAsync("documents");
        await service.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(RagIngestionOperationState.Cancelling, manager.GetStatus("documents").ActiveOperation?.State);
        service.Release.TrySetResult();
        await cancellation;

        Assert.Equal(RagIngestionOperationState.Cancelled, (await operation).State);
        Assert.Equal(RagIngestionOperationState.Cancelled, manager.GetStatus("documents").LastOperation?.State);
    }

    // Verifies cancellation after completion cannot reverse the terminal operation snapshot.
    [Fact]
    public async Task CancelAsync_ShouldPreserveCompletedState_WhenCompletionWins()
    {
        using var provider = CreateProvider(new ControlledRagService());
        var manager = provider.GetRequiredService<IRagIngestionManager>();
        var completed = await manager.StartAsync("documents");

        await manager.CancelAsync("documents");

        Assert.Equal(RagIngestionOperationState.Completed, completed.State);
        Assert.Equal(RagIngestionOperationState.Completed, manager.GetStatus("documents").LastOperation?.State);
    }

    // Verifies concurrent cancellation requests share one cancellation signal and one terminal state.
    [Fact]
    public async Task CancelAsync_ShouldRemainDeterministic_WhenCalledConcurrently()
    {
        var service = new ControlledRagService(block: true);
        using var provider = CreateProvider(service);
        var manager = provider.GetRequiredService<IRagIngestionManager>();
        var operation = manager.StartAsync("documents");
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Task.WhenAll(manager.CancelAsync("documents"), manager.CancelAsync("documents"));

        Assert.Equal(1, service.CancellationCount);
        Assert.Equal(RagIngestionOperationState.Cancelled, (await operation).State);
    }

    // Verifies scheduled and manual triggers cannot create parallel pipelines for one index and a later run remains possible.
    [Fact]
    public async Task StartAsync_ShouldCoordinateScheduledAndManualTriggerRace()
    {
        var service = new ControlledRagService(block: true);
        using var provider = CreateProvider(service);
        var manager = provider.GetRequiredService<RagIngestionManager>();
        var scheduled = manager.StartAsync("documents", RagIngestionOperationReason.Scheduled);
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.StartAsync("documents"));
        Assert.Equal(RagIngestionOperationReason.Scheduled, manager.GetStatus("documents").ActiveOperation?.Reason);
        Assert.Equal(1, service.InvocationCount);
        service.Release.TrySetResult();
        await scheduled;

        var next = await manager.StartAsync("documents");
        Assert.Equal(RagIngestionOperationReason.Manual, next.Reason);
        Assert.Equal(2, service.InvocationCount);
    }

    // Verifies a scheduled tick is skipped while a manual operation owns the index coordination slot.
    [Fact]
    public async Task StartAsync_ShouldRejectScheduledTick_WhenManualOperationIsActive()
    {
        var service = new ControlledRagService(block: true);
        using var provider = CreateProvider(service);
        var manager = provider.GetRequiredService<RagIngestionManager>();
        var manual = manager.StartAsync("documents");
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.StartAsync("documents", RagIngestionOperationReason.Scheduled));

        Assert.Equal(RagIngestionOperationReason.Manual, manager.GetStatus("documents").ActiveOperation?.Reason);
        Assert.Equal(1, service.InvocationCount);
        service.Release.TrySetResult();
        await manual;
    }

    // Verifies that document-level failures yield partial completion with safe failure metadata.
    [Fact]
    public async Task StartAsync_ShouldProducePartialCompletionForDocumentFailures()
    {
        using var provider = CreateProvider(new ControlledRagService(failDocument: true));

        var operation = await provider.GetRequiredService<IRagIngestionManager>().StartAsync("documents");

        Assert.Equal(RagIngestionOperationState.PartiallyCompleted, operation.State);
        Assert.Equal("DocumentFailed", operation.Progress.LastFailure!.Code);
        Assert.DoesNotContain("secret", operation.Progress.LastFailure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(RagIndexReadiness.Degraded, provider.GetRequiredService<IRagIngestionManager>().GetStatus("documents").Readiness);
    }

    // Verifies that applications without named indexes do not register managed hosted execution.
    [Fact]
    public void AddRuniqRag_ShouldNotRegisterHostedRuntimeWithoutNamedIndexes()
    {
        var services = new ServiceCollection();
        services.AddRuniqRag();

        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService));
    }

    private static ServiceProvider CreateProvider(ControlledRagService service)
    {
        var services = new ServiceCollection();
        services.AddRuniqRag(rag => rag.AddIndex("documents", index => index.AddSource(new TestSource()).UseVectorStore("store").UseEmbeddingModel("model")));
        services.Replace(ServiceDescriptor.Scoped<IRagService>(_ => service));
        return services.BuildServiceProvider();
    }

    private sealed class TestSource : IRagDocumentSource
    {
        public string Identity => "safe-source";
        public Task<IReadOnlyList<RagSourceDocument>> GetDocumentsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<RagSourceDocument>>([]);
    }

    private sealed class ControlledRagService(bool block = false, bool failDocument = false, bool holdCancellation = false) : IRagService
    {
        private int cancellationCount;
        private int invocationCount;
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource CancellationObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int CancellationCount => Volatile.Read(ref cancellationCount);
        public int InvocationCount => Volatile.Read(ref invocationCount);
        public async Task<RagIngestionReport> IngestAsync(IRagDocumentSource source, string indexName, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref invocationCount);
            if (block)
            {
                Started.TrySetResult();
                try
                {
                    await Release.Task.WaitAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    Interlocked.Increment(ref cancellationCount);
                    CancellationObserved.TrySetResult();
                    if (holdCancellation) await Release.Task;
                    throw;
                }
            }
            else Started.TrySetResult();
            return new RagIngestionReport { DiscoveredDocuments = 2, CreatedDocuments = 1, SkippedDocuments = failDocument ? 0 : 1, FailedDocuments = failDocument ? 1 : 0, CreatedChunks = 3, Failures = failDocument ? [new RagIngestionFailure { DocumentId = "document", Message = "Safe failure" }] : [] };
        }
        public Task<RagContext> GetContextAsync(RagQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}

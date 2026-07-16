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

    private sealed class ControlledRagService(bool block = false, bool failDocument = false) : IRagService
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<RagIngestionReport> IngestAsync(IRagDocumentSource source, string indexName, CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            if (block) await Release.Task.WaitAsync(cancellationToken);
            return new RagIngestionReport { DiscoveredDocuments = 2, CreatedDocuments = 1, SkippedDocuments = failDocument ? 0 : 1, FailedDocuments = failDocument ? 1 : 0, CreatedChunks = 3, Failures = failDocument ? [new RagIngestionFailure { DocumentId = "document", Message = "Safe failure" }] : [] };
        }
        public Task<RagContext> GetContextAsync(RagQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}

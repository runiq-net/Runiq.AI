using Microsoft.Extensions.DependencyInjection;
using Runiq.AI.Core.Rag;
using Runiq.AI.Rag.Abstractions.Telemetry;
using Runiq.AI.Rag.Configuration;
using Runiq.AI.Rag.DependencyInjection;
using Runiq.AI.Rag.Models.Retrieval;
using Runiq.AI.Rag.Models.Telemetry;
using Runiq.AI.Rag.Models.VectorStores;

namespace Runiq.AI.Core.Tests.Dashboard;

public sealed class RuniqRagInfoReaderTests
{
    [Fact]
    public async Task GetInfoAsync_returns_disabled_payload_when_rag_is_not_registered()
    {
        // Verifies that the reader degrades gracefully to a well-formed Enabled=false payload
        // with a developer-readable diagnostic when no RAG services are registered.
        var services = new ServiceCollection().BuildServiceProvider();
        var reader = new RuniqRagInfoReader(services);

        var info = await reader.GetInfoAsync();

        Assert.False(info.Enabled);
        Assert.Null(info.VectorStore);
        Assert.Null(info.IndexName);
        Assert.Null(info.DefaultTopK);
        Assert.Null(info.EmbeddingDimension);
        Assert.NotNull(info.Diagnostics);
        Assert.Contains("not registered", info.Diagnostics, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetInfoAsync_maps_configured_rag_options()
    {
        // Verifies that configuration-derived fields (index name, default top-K) are read from the
        // registered RagOptions and that the unavailable embedding dimension stays null.
        var services = new ServiceCollection();
        services.AddRuniqRag();
        services.Configure<RagOptions>(options =>
        {
            options.DefaultIndexName = "docs-index";
            options.DefaultTopK = 7;
        });

        var reader = new RuniqRagInfoReader(services.BuildServiceProvider());

        var info = await reader.GetInfoAsync();

        Assert.True(info.Enabled);
        Assert.Equal("docs-index", info.IndexName);
        Assert.Equal(7, info.DefaultTopK);
        Assert.Null(info.EmbeddingDimension);
        Assert.Null(info.LastUpsert);
        Assert.Null(info.LastRetrieval);
    }

    [Fact]
    public async Task GetInfoAsync_maps_recorded_rag_operation_telemetry()
    {
        // Verifies that Core maps already-recorded RAG telemetry into dashboard DTO fields without
        // executing any RAG operation or fabricating values.
        var upsertTimestamp = new DateTimeOffset(2026, 7, 4, 10, 30, 0, TimeSpan.Zero);
        var retrievalTimestamp = new DateTimeOffset(2026, 7, 4, 10, 31, 0, TimeSpan.Zero);
        var services = new ServiceCollection();
        services.AddSingleton<IRagOperationTelemetryReader>(
            new FakeRagOperationTelemetryReader
            {
                LastUpsert = new RagLastUpsertTelemetry
                {
                    Succeeded = false,
                    ErrorCode = VectorStoreUpsertErrorCode.StoreFailed,
                    Reason = "Vector store rejected the batch.",
                    ChunkCount = 12,
                    Timestamp = upsertTimestamp
                },
                LastRetrieval = new RagLastRetrievalTelemetry
                {
                    Succeeded = true,
                    ErrorCode = RetrievalErrorCode.None,
                    Reason = string.Empty,
                    ResultCount = 3,
                    Duration = TimeSpan.FromMilliseconds(42.5),
                    Timestamp = retrievalTimestamp
                }
            });
        services.AddRuniqRag();
        services.AddInMemoryRagVectorStore();

        var reader = new RuniqRagInfoReader(services.BuildServiceProvider());

        var info = await reader.GetInfoAsync();

        Assert.NotNull(info.LastUpsert);
        Assert.False(info.LastUpsert.Succeeded);
        Assert.Equal("StoreFailed", info.LastUpsert.ErrorCode);
        Assert.Equal("Vector store rejected the batch.", info.LastUpsert.Reason);
        Assert.Equal(12, info.LastUpsert.ChunkCount);
        Assert.Equal(upsertTimestamp, info.LastUpsert.Timestamp);

        Assert.NotNull(info.LastRetrieval);
        Assert.True(info.LastRetrieval.Succeeded);
        Assert.Equal("None", info.LastRetrieval.ErrorCode);
        Assert.Equal(string.Empty, info.LastRetrieval.Reason);
        Assert.Equal(3, info.LastRetrieval.ResultCount);
        Assert.Equal(42.5, info.LastRetrieval.DurationMilliseconds);
        Assert.Equal(retrievalTimestamp, info.LastRetrieval.Timestamp);
    }

    [Fact]
    public async Task GetInfoAsync_reports_null_vector_store_with_diagnostic()
    {
        // Verifies that the default null vector store registered by AddRuniqRag is surfaced as the
        // provider label together with a developer-readable configuration diagnostic.
        var services = new ServiceCollection();
        services.AddRuniqRag();

        var reader = new RuniqRagInfoReader(services.BuildServiceProvider());

        var info = await reader.GetInfoAsync();

        Assert.True(info.Enabled);
        Assert.Equal("NullVectorStore", info.VectorStore);
        Assert.NotNull(info.Diagnostics);
        Assert.Contains("vector store", info.Diagnostics, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetInfoAsync_reports_configured_vector_store_provider_label()
    {
        // Verifies that a configured vector store provider is reported by its provider type name
        // (unwrapped from the validating decorator) and produces no configuration diagnostic.
        var services = new ServiceCollection();
        services.AddRuniqRag();
        services.AddInMemoryRagVectorStore();

        var reader = new RuniqRagInfoReader(services.BuildServiceProvider());

        var info = await reader.GetInfoAsync();

        Assert.True(info.Enabled);
        Assert.Equal("InMemoryRagVectorStore", info.VectorStore);
        Assert.Null(info.Diagnostics);
    }

    [Fact]
    public async Task GetInfoAsync_observes_cancellation_before_reading()
    {
        // Verifies that an already-cancelled token cancels the read instead of returning a payload.
        var services = new ServiceCollection().BuildServiceProvider();
        var reader = new RuniqRagInfoReader(services);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => reader.GetInfoAsync(new CancellationToken(canceled: true)));
    }

    [Fact]
    public void AddRuniqServer_registers_replaceable_rag_info_provider()
    {
        // Verifies that AddRuniqServer registers the RAG info provider so the dashboard endpoint can
        // resolve it, and that a host registration placed before AddRuniqServer takes precedence.
        var services = new ServiceCollection();
        services.AddRuniqServer();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.IsType<RuniqRagInfoReader>(
            scope.ServiceProvider.GetRequiredService<IRuniqRagInfoProvider>());

        var overridingServices = new ServiceCollection();
        overridingServices.AddScoped<IRuniqRagInfoProvider, FakeRagInfoProvider>();
        overridingServices.AddRuniqServer();

        using var overridingProvider = overridingServices.BuildServiceProvider();
        using var overridingScope = overridingProvider.CreateScope();

        Assert.IsType<FakeRagInfoProvider>(
            overridingScope.ServiceProvider.GetRequiredService<IRuniqRagInfoProvider>());
    }

    private sealed class FakeRagInfoProvider : IRuniqRagInfoProvider
    {
        public Task<RuniqRagInfo> GetInfoAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new RuniqRagInfo());
        }
    }

    private sealed class FakeRagOperationTelemetryReader : IRagOperationTelemetryReader
    {
        public RagLastUpsertTelemetry? LastUpsert { get; init; }

        public RagLastRetrievalTelemetry? LastRetrieval { get; init; }
    }
}


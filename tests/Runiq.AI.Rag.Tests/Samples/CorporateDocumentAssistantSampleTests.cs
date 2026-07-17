using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Runiq.AI.Agents;
using Runiq.AI.Agents.Providers.OpenAI;
using Runiq.AI.Core.AI.Embeddings;
using Runiq.AI.Core.Models;
using Runiq.AI.Rag.Abstractions.Retrieval;
using Runiq.AI.Rag.Abstractions.VectorStores;
using Runiq.AI.Rag.Configuration;
using Runiq.AI.Rag.CorporateDocumentAssistant;
using Runiq.AI.Rag.CorporateDocumentAssistant.Services;
using Runiq.AI.Rag.DependencyInjection;
using Runiq.AI.Rag.Ingestion;
using Runiq.AI.Rag.Models.Retrieval;
using Runiq.AI.Rag.Runtime;

namespace Runiq.AI.Rag.Tests.Samples;

/// <summary>Covers the production registration and managed startup behavior demonstrated by the Corporate Document Assistant.</summary>
public sealed class CorporateDocumentAssistantSampleTests
{
    // Verifies the sample registers one typed OpenAI-backed OnStartup index and points the agent at the same effective name.
    [Fact]
    public void Configure_ShouldRegisterCorporateIndexAndGroundedAgent()
    {
        using var documents = TestDocuments.Create();
        using var provider = CreateProvider(documents.Path, startIngestion: false);
        var registration = Assert.Single(provider.GetRequiredService<IRagIndexRegistry>().Registrations);
        var agent = Assert.Single(provider.GetServices<Agent>());

        Assert.Equal(CorporateDocumentAssistantSetup.IndexName, registration.Name);
        Assert.Equal(RagIngestionStrategyKind.OnStartup, registration.IngestionStrategy.Kind);
        Assert.Equal(OpenAiEmbeddingModels.TextEmbedding3Small.Reference, registration.EmbeddingReference);
        Assert.Equal("InMemory", registration.VectorStoreType);
        Assert.Equal(CorporateDocumentAssistantSetup.IndexName, agent.Rag?.IndexName);
        Assert.Equal(CorporateDocumentAssistantSetup.ChatModel, agent.Model);
        var source = Assert.IsType<DirectoryRagDocumentSource>(Assert.Single(registration.Sources));
        Assert.Equal(Path.GetFullPath(documents.Path), source.RootPath);
    }

    // Verifies managed startup ingestion discovers bundled documents and leaves a completed, ready runtime snapshot without a seed request.
    [Fact]
    public async Task OnStartup_ShouldIngestDocumentsAndBecomeReady()
    {
        using var documents = TestDocuments.Create();
        await using var provider = CreateProvider(documents.Path, startIngestion: false);

        await StartHostedServicesAsync(provider);

        var status = provider.GetRequiredService<IRagIngestionManager>().GetStatus(CorporateDocumentAssistantSetup.IndexName);
        Assert.Equal(RagIndexReadiness.Ready, status.Readiness);
        Assert.Equal(RagIngestionOperationState.Completed, status.LastOperation?.State);
        Assert.Equal(2, status.LastOperation?.Progress.DiscoveredDocuments);
        Assert.True(status.LastOperation?.Progress.ProducedChunks > 0);
        Assert.Equal(status.LastOperation?.Progress.ProducedChunks, status.LastOperation?.Progress.ProducedEmbeddings);
    }

    // Verifies ingestion and query-time retrieval share the same in-memory store and select the known remote-work document.
    [Fact]
    public async Task Retrieval_ShouldUseStartupIndexAndReturnKnownDocument()
    {
        using var documents = TestDocuments.Create();
        await using var provider = CreateProvider(documents.Path, startIngestion: false);
        await StartHostedServicesAsync(provider);
        using var scope = provider.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IRagRetrievalPipeline>().RetrieveAsync(new RetrievalRequest
        {
            IndexName = CorporateDocumentAssistantSetup.IndexName,
            QueryText = "How many remote work days are allowed?",
            TopK = 2
        });

        Assert.True(result.Succeeded);
        Assert.NotEmpty(result.Items);
        Assert.Contains(result.Items, item => item.Metadata.Values.TryGetValue("documentId", out var documentId) && documentId.Contains("remote-work-policy", StringComparison.Ordinal));
        Assert.Same(provider.GetRequiredService<IRagVectorStore>(), scope.ServiceProvider.GetRequiredService<IRagVectorStore>());
    }

    // Verifies an explicit second managed ingestion observes unchanged document hashes instead of creating duplicate content.
    [Fact]
    public async Task ManualIngestionAfterStartup_ShouldReportSameHashSkips()
    {
        using var documents = TestDocuments.Create();
        await using var provider = CreateProvider(documents.Path, startIngestion: false);
        await StartHostedServicesAsync(provider);

        var operation = await provider.GetRequiredService<IRagIngestionManager>().StartAsync(CorporateDocumentAssistantSetup.IndexName);

        Assert.Equal(RagIngestionOperationState.Completed, operation.State);
        Assert.Equal(2, operation.Progress.SkippedDocuments);
        Assert.Equal(0, operation.Progress.AddedDocuments);
    }

    // Verifies a missing bundled document directory fails managed startup instead of exposing an unusable application.
    [Fact]
    public async Task OnStartup_ShouldFail_WhenDocumentDirectoryIsMissing()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"runiq-missing-{Guid.NewGuid():N}");
        await using var provider = CreateProvider(missingPath, startIngestion: false);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => StartHostedServicesAsync(provider));

        Assert.Contains(CorporateDocumentAssistantSetup.IndexName, exception.Message, StringComparison.Ordinal);
        Assert.Equal(RagIndexReadiness.Failed, provider.GetRequiredService<IRagIngestionManager>().GetStatus(CorporateDocumentAssistantSetup.IndexName).Readiness);
    }

    // Verifies an embedding provider failure fails managed startup and records a safe failed readiness state.
    [Fact]
    public async Task OnStartup_ShouldFail_WhenEmbeddingProviderFails()
    {
        using var documents = TestDocuments.Create();
        await using var provider = CreateProvider<ThrowingEmbeddingClient>(documents.Path);

        await Assert.ThrowsAsync<InvalidOperationException>(() => StartHostedServicesAsync(provider));

        var status = provider.GetRequiredService<IRagIngestionManager>().GetStatus(CorporateDocumentAssistantSetup.IndexName);
        Assert.Equal(RagIndexReadiness.Failed, status.Readiness);
        Assert.Equal("DocumentFailed", status.LastOperation?.Progress.LastFailure?.Code);
        Assert.DoesNotContain("provider-secret", status.LastOperation?.Progress.LastFailure?.Message, StringComparison.Ordinal);
    }

    // Verifies host-start cancellation propagates through managed ingestion instead of being converted into readiness.
    [Fact]
    public async Task OnStartup_ShouldPropagateCancellation()
    {
        using var documents = TestDocuments.Create();
        await using var provider = CreateProvider<BlockingEmbeddingClient>(documents.Path);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => StartHostedServicesAsync(provider, cancellation.Token));

        Assert.Equal(RagIngestionOperationState.Cancelled, provider.GetRequiredService<IRagIngestionManager>().GetStatus(CorporateDocumentAssistantSetup.IndexName).LastOperation?.State);
    }

    // Verifies missing credentials fail configuration before any provider, ingestion, or server work can begin.
    [Fact]
    public void Configure_ShouldFailFast_WhenOpenAiKeyIsMissing()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        var exception = Assert.Throws<InvalidOperationException>(() => CorporateDocumentAssistantSetup.Configure(services, configuration, "documents"));

        Assert.Contains("OpenAI:ApiKey", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-", exception.Message, StringComparison.Ordinal);
    }

    // Verifies the real OpenAI embedding adapter maps ordered vectors and sends credentials only in the authorization header.
    [Fact]
    public async Task OpenAiEmbeddingClient_ShouldMapSafeOrderedResponse()
    {
        const string secret = "test-secret-value";
        var handler = new RecordingHandler("""{"data":[{"index":0,"embedding":[0.1,0.2,0.3]}],"usage":{"prompt_tokens":2,"total_tokens":2}}""");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["OpenAI:ApiKey"] = secret }).Build();
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var provider = new OpenAiEmbeddingClient(client, configuration);

        var response = await provider.EmbedAsync(new EmbeddingRequest(ModelReference.Parse("openai/text-embedding-3-small"), ["hello"]));

        Assert.Equal(3, Assert.Single(response.Results).Dimensions);
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal(secret, handler.AuthorizationParameter);
        Assert.Contains("text-embedding-3-small", handler.RequestBody, StringComparison.Ordinal);
        Assert.DoesNotContain(secret, handler.RequestBody, StringComparison.Ordinal);
    }

    private static ServiceProvider CreateProvider(string documentsPath, bool startIngestion)
    {
        return CreateProvider<KeywordEmbeddingClient>(documentsPath, startIngestion);
    }

    private static ServiceProvider CreateProvider<TEmbeddingClient>(string documentsPath, bool startIngestion = false)
        where TEmbeddingClient : class, IEmbeddingClient
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["OpenAI:ApiKey"] = "test-key" }).Build();
        CorporateDocumentAssistantSetup.Configure(services, configuration, documentsPath);
        services.AddRagEmbeddingClient<TEmbeddingClient>();
        var provider = services.BuildServiceProvider();
        if (startIngestion) StartHostedServicesAsync(provider).GetAwaiter().GetResult();
        return provider;
    }

    private static async Task StartHostedServicesAsync(IServiceProvider provider, CancellationToken cancellationToken = default)
    {
        foreach (var service in provider.GetServices<IHostedService>())
        {
            await service.StartAsync(cancellationToken);
        }
    }

    private sealed class KeywordEmbeddingClient : IEmbeddingClient
    {
        public Task<EmbeddingResponse> EmbedAsync(EmbeddingRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new EmbeddingResponse(request.Inputs.Select((text, index) => new EmbeddingResult(index, Vector(text), 4)).ToArray()));

        private static IReadOnlyList<float> Vector(string text)
        {
            var normalized = text.ToLowerInvariant();
            return [normalized.Contains("remote") || normalized.Contains("work days") ? 1 : 0, normalized.Contains("expense") || normalized.Contains("manager approval") ? 1 : 0, normalized.Contains("security") || normalized.Contains("incident") ? 1 : 0, 0.01f];
        }
    }

    private sealed class ThrowingEmbeddingClient : IEmbeddingClient
    {
        public Task<EmbeddingResponse> EmbedAsync(EmbeddingRequest request, CancellationToken cancellationToken = default) =>
            Task.FromException<EmbeddingResponse>(new InvalidOperationException("provider-secret diagnostic"));
    }

    private sealed class BlockingEmbeddingClient : IEmbeddingClient
    {
        public async Task<EmbeddingResponse> EmbedAsync(EmbeddingRequest request, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Unreachable.");
        }
    }

    private sealed class RecordingHandler(string responseBody) : HttpMessageHandler
    {
        public string? AuthorizationScheme { get; private set; }
        public string? AuthorizationParameter { get; private set; }
        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(responseBody, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class TestDocuments : IDisposable
    {
        private TestDocuments(string path) => Path = path;
        public string Path { get; }

        public static TestDocuments Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"runiq-corporate-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            File.WriteAllText(System.IO.Path.Combine(path, "remote-work-policy.md"), "# Remote Work Policy\nEmployees may work remotely up to three days per week.");
            File.WriteAllText(System.IO.Path.Combine(path, "expense-policy.md"), "# Expense Policy\nExpenses above USD 250 require manager approval.");
            return new TestDocuments(path);
        }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}

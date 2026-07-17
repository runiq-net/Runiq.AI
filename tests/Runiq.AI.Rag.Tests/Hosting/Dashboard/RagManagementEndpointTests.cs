using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Runiq.AI.Core;
using Runiq.AI.Agents;
using Runiq.AI.Rag.Abstractions.Ingestion;
using Runiq.AI.Rag.Abstractions.Services;
using Runiq.AI.Rag.Configuration;
using Runiq.AI.Rag.DependencyInjection;
using Runiq.AI.Rag.Models.Context;
using Runiq.AI.Rag.Models.Ingestion;
using Runiq.AI.Rag.Models.Queries;

namespace Runiq.AI.Rag.Tests.Hosting.Dashboard;

/// <summary>Validates the dashboard RAG management HTTP contract.</summary>
[Collection("Dashboard assets")]
public sealed class RagManagementEndpointTests
{
    /// <summary>Verifies that listing combines safe registry metadata with independent runtime state.</summary>
    [Fact]
    // Verifies safe static metadata and runtime readiness are combined without leaking provider configuration.
    public async Task List_ShouldProjectSafeConfigurationAndRuntimeState()
    {
        using var server = CreateServer();
        var response = await server.GetTestClient().GetAsync("/dashboard/api/rag/indexes");
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var index = Assert.Single(json.RootElement.EnumerateArray());
        Assert.Equal("documents", index.GetProperty("name").GetString());
        Assert.Equal("NotInitialized", index.GetProperty("readiness").GetString());
        Assert.Equal("Scheduled", index.GetProperty("configuration").GetProperty("ingestionStrategy").GetString());
        Assert.Equal("safe-store", index.GetProperty("configuration").GetProperty("vectorStoreReference").GetString());
        Assert.DoesNotContain("C:\\secret", json.RootElement.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", json.RootElement.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifies that unknown index detail requests return a structured not-found response.</summary>
    [Fact]
    // Verifies unknown index details use the stable structured not-found contract.
    public async Task Detail_ShouldReturnStructuredNotFound_ForUnknownIndex()
    {
        using var server = CreateServer();
        var response = await server.GetTestClient().GetAsync("/dashboard/api/rag/indexes/missing");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("RagIndexNotFound", json.RootElement.GetProperty("code").GetString());
    }

    /// <summary>Verifies that explicit manual ingestion is allowed for a scheduled index and uses the managed runtime.</summary>
    [Fact]
    // Verifies automatic strategy selection does not prevent an explicit managed ingestion request.
    public async Task Start_ShouldAllowExplicitRun_ForScheduledIndex()
    {
        var service = new ControlledRagService(block: false);
        using var server = CreateServer(service);
        var response = await server.GetTestClient().PostAsync("/dashboard/api/rag/indexes/documents/ingestion/start", null);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("documents", json.RootElement.GetProperty("indexName").GetString());
        Assert.Equal("Manual", json.RootElement.GetProperty("reason").GetString());
        Assert.True(service.WasCalled);
    }

    /// <summary>Verifies that concurrent starts for one index produce a structured conflict with the active operation.</summary>
    [Fact]
    // Verifies the manager remains the concurrency authority for competing start requests.
    public async Task Start_ShouldReturnConflict_WhenOperationIsActive()
    {
        using var server = CreateServer(new ControlledRagService(block: true));
        var client = server.GetTestClient();
        var first = await client.PostAsync("/dashboard/api/rag/indexes/documents/ingestion/start", null);
        var second = await client.PostAsync("/dashboard/api/rag/indexes/documents/ingestion/start", null);
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        using var json = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        Assert.Equal("ActiveIngestionOperation", json.RootElement.GetProperty("code").GetString());
        Assert.Equal("Running", json.RootElement.GetProperty("activeOperation").GetProperty("state").GetString());
    }

    /// <summary>Verifies that cancellation reaches the manager and returns the terminal cancelled snapshot.</summary>
    [Fact]
    // Verifies the cancellation endpoint returns the manager's safe terminal operation snapshot.
    public async Task Cancel_ShouldReturnCancelledOperation_WhenOperationIsActive()
    {
        using var server = CreateServer(new ControlledRagService(block: true));
        var client = server.GetTestClient();
        await client.PostAsync("/dashboard/api/rag/indexes/documents/ingestion/start", null);
        var response = await client.PostAsync("/dashboard/api/rag/indexes/documents/ingestion/cancel", null);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Cancelled", json.RootElement.GetProperty("state").GetString());
    }

    /// <summary>Verifies that cancelling without an active operation returns a deterministic structured conflict.</summary>
    [Fact]
    // Verifies cancellation without active work has a deterministic machine-readable conflict.
    public async Task Cancel_ShouldReturnConflict_WhenNoOperationIsActive()
    {
        using var server = CreateServer();
        var response = await server.GetTestClient().PostAsync("/dashboard/api/rag/indexes/documents/ingestion/cancel", null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("NoActiveIngestionOperation", json.RootElement.GetProperty("code").GetString());
    }

    /// <summary>Verifies that RAG management routes inherit the Dashboard authorization boundary.</summary>
    [Fact]
    // Verifies anonymous callers cannot bypass an authenticated Dashboard policy through a management route.
    public async Task List_ShouldReturnUnauthorized_WhenDashboardRequiresAuthentication()
    {
        using var server = CreateServer(requireAuthentication: true);
        var response = await server.GetTestClient().GetAsync("/dashboard/api/rag/indexes");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static IHost CreateServer(ControlledRagService? service = null, bool requireAuthentication = false) => new HostBuilder()
        .ConfigureWebHost(web => web.UseTestServer().ConfigureServices(services =>
        {
            services.AddRouting();
            services.AddRuniqServer();
            services.AddRuniqRag(rag => rag.AddIndex("documents", index => index
                .AddSource(new SafeSource())
                .UseVectorStore(new RagVectorStoreReference("postgres://user:password@host/db", "PostgreSql", "safe-store"))
                .UseEmbeddingModel(new RagEmbeddingModelReference("provider", "secret-model", "safe-embedding"))
                .ConfigureChunking(512, 64)
                .ConfigureIngestion(strategy => strategy.Scheduled("0 * * * *"))));
            services.AddScoped<IRagService>(_ => service ?? new ControlledRagService(block: false));
        }).Configure(app => app.UseRuniqDashboard(options =>
        {
            options.Path = "/dashboard";
            options.Authentication(authentication =>
            {
                if (requireAuthentication) authentication.RequireAuthenticatedUser();
                else authentication.AllowAnonymous();
            });
        }))).Start();

    private sealed class SafeSource : IRagDocumentSource
    {
        public string Identity => "source-1";
        public string SourceType => "Directory";
        public string DisplayValue => "documents";
        public Task<IReadOnlyList<RagSourceDocument>> GetDocumentsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<RagSourceDocument>>([]);
    }

    private sealed class ControlledRagService(bool block) : IRagService
    {
        public bool WasCalled { get; private set; }
        public async Task<RagIngestionReport> IngestAsync(IRagDocumentSource source, string indexName, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            if (block) await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new RagIngestionReport();
        }
        public Task<RagContext> GetContextAsync(RagQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}

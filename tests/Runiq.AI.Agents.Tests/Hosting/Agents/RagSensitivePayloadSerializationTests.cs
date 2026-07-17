using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Runiq.AI.Agents;
using Runiq.AI.Agents.Configuration;
using Runiq.AI.Agents.Runtime;
using Runiq.AI.Core.Agents;
using Runiq.AI.Rag.Abstractions.Observability;
using Runiq.AI.Rag.Configuration;
using Runiq.AI.Rag.Models.Retrieval;

namespace Runiq.AI.Core.Tests.Agents;

public sealed class RagSensitivePayloadSerializationTests
{
    private static readonly string[] Sentinels =
    [
        "sk-api-key-sentinel", "Host=db;Password=connection-string-sentinel", "C:\\absolute\\secret.txt",
        "full-query-sentinel", "full-chunk-content-sentinel", "provider-diagnostic-sentinel",
        "exception-stack-sentinel", "tenant-id-sentinel", "user-id-sentinel", "role-claim-sentinel",
        "access-filter-sentinel", "custom-metadata-secret-sentinel",
    ];

    // Verifies started, completed, and failed SSE JSON omit every sensitive sentinel at the serializer boundary.
    [Fact]
    public void RagLifecycleSerialization_ShouldContainOnlySafeProjection()
    {
        var options = new RagObservabilityOptions { QueryVisibility = RagQueryVisibility.Hidden };
        options.ContentPreview.Enabled = true;
        options.ContentPreview.IncludeSelectedResults = true;
        options.ContentPreview.MaximumCharacters = 12;
        options.SafeMetadataKeys.Add("title");
        var projection = new RagObservabilityProjection(Options.Create(options), new SentinelRedactor(), null,
            NullLogger<RagObservabilityProjection>.Instance);
        var queries = projection.ProjectQueries("full-query-sentinel", null);
        var preview = projection.ProjectContent("full-chunk-content-sentinel and public", selected: true);
        var metadata = projection.ProjectMetadata(new Dictionary<string, string>
        {
            ["title"] = "safe title",
            ["tenant"] = "tenant-id-sentinel",
            ["path"] = "C:\\absolute\\secret.txt",
            ["custom"] = "custom-metadata-secret-sentinel",
        });
        AgentExecutionEvent[] events =
        [
            AgentExecutionEvent.FromRagSearch(new RagSearchStarted("correlation", "agent", "conversation", "index", queries.Original, queries.Effective, 1)),
            AgentExecutionEvent.FromRagSearch(new RagSearchCompleted("correlation", "agent", "conversation", "index", queries.Original, queries.Effective,
                1, 1, 1, 0, [new RagSearchSelectedResult("document", "chunk", 1, 1, "cosine-similarity", true, preview.Value, preview.Truncated, metadata)],
                [], 1, TimeSpan.FromMilliseconds(1), 1, 1, null)),
            AgentExecutionEvent.FromRagSearch(new RagSearchFailed("correlation", "agent", "conversation", "index", queries.Original, queries.Effective,
                1, RetrievalErrorCode.RetrievalFailed, TimeSpan.FromMilliseconds(1))),
            AgentExecutionEvent.Completed(null, [new AgentCitation(1, "document", "chunk", "correlation", 0, 1)]),
            AgentExecutionEvent.Failed("Safe failure.", "RagRetrievalFailed"),
        ];

        var sse = string.Join("\n", events.Select(item => $"data: {JsonSerializer.Serialize(AgentChatStreamEventMapper.FromExecutionEvent(item), new JsonSerializerOptions(JsonSerializerDefaults.Web))}"));

        Assert.Contains("safe title", sse);
        Assert.Contains("[REMOVED]", sse);
        foreach (var sentinel in Sentinels) Assert.DoesNotContain(sentinel, sse, StringComparison.Ordinal);
        Assert.DoesNotContain("stackTrace", sse, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("providerResponse", sse, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class SentinelRedactor : IRagObservabilityRedactor
    {
        public string? Redact(string value, RagObservabilityValueKind kind) =>
            value.Contains("full-chunk-content-sentinel", StringComparison.Ordinal)
                ? value.Replace("full-chunk-content-sentinel", "[REMOVED]", StringComparison.Ordinal)
                : value;
    }
}

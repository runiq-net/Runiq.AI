using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Runiq.AI.Agents.Runtime;
using Runiq.AI.Rag.Abstractions.Observability;
using Runiq.AI.Rag.Configuration;

namespace Runiq.AI.Agents.Tests.Runtime;

public sealed class RagObservabilityProjectionTests
{
    // Verifies that hidden query mode omits both original and effective queries.
    [Fact]
    public void ProjectQueries_ShouldOmitQueries_WhenHidden()
    {
        var projection = Create(new RagObservabilityOptions { QueryVisibility = RagQueryVisibility.Hidden });

        var result = projection.ProjectQueries("secret original", "secret effective");

        Assert.Null(result.Original);
        Assert.Null(result.Effective);
    }

    // Verifies that redaction runs before Unicode-safe truncation and control normalization.
    [Fact]
    public void ProjectContent_ShouldRedactBeforeUnicodeSafeTruncation()
    {
        var options = new RagObservabilityOptions();
        options.ContentPreview.Enabled = true;
        options.ContentPreview.IncludeSelectedResults = true;
        options.ContentPreview.MaximumCharacters = 5;
        var projection = Create(options, new ReplacingRedactor());

        var result = projection.ProjectContent("secret\n👩🏽‍💻tail", selected: true);

        Assert.DoesNotContain("secret", result.Value);
        Assert.Equal("safe…", result.Value);
        Assert.True(result.Truncated);
    }

    // Verifies that redactor exceptions omit raw values instead of failing execution or falling back to them.
    [Fact]
    public void Projection_ShouldUseSafeFallback_WhenRedactorThrows()
    {
        var projection = Create(new RagObservabilityOptions(), new ThrowingRedactor());

        var result = projection.ProjectQueries("raw secret", null);

        Assert.Null(result.Original);
    }

    // Verifies that only explicitly allowlisted metadata is emitted with deterministic bounds.
    [Fact]
    public void ProjectMetadata_ShouldAllowlistAndBoundStringValues()
    {
        var options = new RagObservabilityOptions { MetadataEntryLimit = 1, MaximumMetadataValueLength = 5 };
        options.SafeMetadataKeys.Add("title");
        var projection = Create(options);

        var result = projection.ProjectMetadata(new Dictionary<string, string>
        {
            ["tenantId"] = "tenant-sentinel",
            ["title"] = "approved title",
            ["path"] = "C:\\secret\\file.txt",
        });

        Assert.Equal("appr…", result["title"]);
        Assert.Single(result);
    }

    // Verifies that projector failures produce empty metadata without a raw fallback.
    [Fact]
    public void ProjectMetadata_ShouldUseEmptyFallback_WhenProjectorThrows()
    {
        var projection = Create(new RagObservabilityOptions(), metadataProjector: new ThrowingMetadataProjector());

        var result = projection.ProjectMetadata(new Dictionary<string, string> { ["secret"] = "metadata-sentinel" });

        Assert.Empty(result);
    }

    // Verifies that runtime projection uses an immutable effective options snapshot.
    [Fact]
    public void Projection_ShouldSnapshotOptions()
    {
        var options = new RagObservabilityOptions { QueryVisibility = RagQueryVisibility.Redacted };
        var projection = Create(options);
        options.QueryVisibility = RagQueryVisibility.Visible;

        var result = projection.ProjectQueries("raw-query-sentinel", null);

        Assert.Equal("[REDACTED]", result.Original);
    }

    private static RagObservabilityProjection Create(RagObservabilityOptions options,
        IRagObservabilityRedactor? redactor = null,
        IRagObservabilityMetadataProjector? metadataProjector = null) =>
        new(Options.Create(options), redactor, metadataProjector, NullLogger<RagObservabilityProjection>.Instance);

    private sealed class ReplacingRedactor : IRagObservabilityRedactor
    {
        public string? Redact(string value, RagObservabilityValueKind kind) => value.Replace("secret", "safe", StringComparison.Ordinal);
    }

    private sealed class ThrowingRedactor : IRagObservabilityRedactor
    {
        public string? Redact(string value, RagObservabilityValueKind kind) => throw new InvalidOperationException("provider diagnostic sentinel");
    }

    private sealed class ThrowingMetadataProjector : IRagObservabilityMetadataProjector
    {
        public IReadOnlyDictionary<string, string>? Project(IReadOnlyDictionary<string, string> metadata) =>
            throw new InvalidOperationException("provider diagnostic sentinel");
    }
}

using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Runiq.AI.Rag.Abstractions.Observability;
using Runiq.AI.Rag.Configuration;

namespace Runiq.AI.Agents.Runtime;

internal sealed class RagObservabilityProjection
{
    private const string TruncationMarker = "…";
    private readonly RagObservabilityOptions options;
    private readonly IRagObservabilityRedactor? redactor;
    private readonly IRagObservabilityMetadataProjector? metadataProjector;
    private readonly ILogger logger;

    public RagObservabilityProjection(IOptions<RagObservabilityOptions> options,
        IRagObservabilityRedactor? redactor, IRagObservabilityMetadataProjector? metadataProjector,
        ILogger<RagObservabilityProjection> logger)
    {
        this.options = Snapshot(options.Value);
        this.redactor = redactor;
        this.metadataProjector = metadataProjector;
        this.logger = logger;
    }

    public IReadOnlyDictionary<string, string> ProjectMetadata(IReadOnlyDictionary<string, string> metadata)
    {
        if (metadata.Count == 0 || options.MetadataEntryLimit == 0) return new Dictionary<string, string>();
        IEnumerable<KeyValuePair<string, string>> candidates;
        try
        {
            candidates = metadataProjector is null
                ? metadata.Where(pair => options.SafeMetadataKeys.Contains(pair.Key))
                : metadataProjector.Project(metadata) ?? new Dictionary<string, string>();
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "A RAG observability metadata projector failed; metadata was omitted.");
            return new Dictionary<string, string>();
        }

        var projected = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in candidates.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (projected.Count >= options.MetadataEntryLimit) break;
            if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value) || projected.ContainsKey(pair.Key)) continue;
            var safe = ApplyRedactor(pair.Value, RagObservabilityValueKind.Metadata);
            if (string.IsNullOrWhiteSpace(safe)) continue;
            projected[pair.Key] = Truncate(Normalize(safe), options.MaximumMetadataValueLength).Value;
        }
        return projected;
    }

    public (string? Original, string? Effective) ProjectQueries(string original, string? effective)
    {
        var projectedOriginal = ProjectQuery(original);
        var projectedEffective = string.IsNullOrWhiteSpace(effective) || string.Equals(original, effective, StringComparison.Ordinal)
            ? null : ProjectQuery(effective);
        if (string.Equals(projectedOriginal, projectedEffective, StringComparison.Ordinal)) projectedEffective = null;
        return (projectedOriginal, projectedEffective);
    }

    public (string? Value, bool Truncated) ProjectContent(string? content, bool selected)
    {
        var preview = options.ContentPreview;
        if (!preview.Enabled || selected && !preview.IncludeSelectedResults || !selected && !preview.IncludeRejectedResults || string.IsNullOrEmpty(content))
            return (null, false);
        var safe = ApplyRedactor(content, RagObservabilityValueKind.Content);
        if (string.IsNullOrWhiteSpace(safe)) return (null, false);
        safe = Normalize(safe);
        return Truncate(safe, preview.MaximumCharacters);
    }

    private string? ProjectQuery(string value)
    {
        if (options.QueryVisibility == RagQueryVisibility.Hidden) return null;
        if (options.QueryVisibility == RagQueryVisibility.Redacted) return options.RedactedQueryPlaceholder;
        var safe = ApplyRedactor(value, RagObservabilityValueKind.Query);
        return string.IsNullOrWhiteSpace(safe) ? null : Truncate(Normalize(safe), options.MaximumQueryCharacters).Value;
    }

    private string? ApplyRedactor(string value, RagObservabilityValueKind kind)
    {
        if (redactor is null) return value;
        try { return redactor.Redact(value, kind); }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "A RAG observability redactor failed for {ValueKind}; the value was omitted.", kind);
            return null;
        }
    }

    private static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length);
        var previousWhitespace = false;
        foreach (var rune in value.EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(rune);
            var whitespace = Rune.IsWhiteSpace(rune) || category is UnicodeCategory.Control or UnicodeCategory.Format;
            if (whitespace)
            {
                if (!previousWhitespace) builder.Append(' ');
                previousWhitespace = true;
            }
            else { builder.Append(rune.ToString()); previousWhitespace = false; }
        }
        return builder.ToString().Trim();
    }

    private static (string Value, bool Truncated) Truncate(string value, int maximumCharacters)
    {
        var elements = StringInfo.ParseCombiningCharacters(value);
        if (elements.Length <= maximumCharacters) return (value, false);
        var kept = Math.Max(0, maximumCharacters - 1);
        var end = kept == 0 ? 0 : elements[kept];
        return (value[..end] + TruncationMarker, true);
    }

    private static RagObservabilityOptions Snapshot(RagObservabilityOptions source)
    {
        var snapshot = new RagObservabilityOptions
        {
            QueryVisibility = source.QueryVisibility,
            RedactedQueryPlaceholder = source.RedactedQueryPlaceholder,
            MaximumQueryCharacters = source.MaximumQueryCharacters,
            MetadataEntryLimit = source.MetadataEntryLimit,
            MaximumMetadataValueLength = source.MaximumMetadataValueLength,
        };
        foreach (var key in source.SafeMetadataKeys) snapshot.SafeMetadataKeys.Add(key);
        snapshot.ContentPreview.Enabled = source.ContentPreview.Enabled;
        snapshot.ContentPreview.MaximumCharacters = source.ContentPreview.MaximumCharacters;
        snapshot.ContentPreview.IncludeSelectedResults = source.ContentPreview.IncludeSelectedResults;
        snapshot.ContentPreview.IncludeRejectedResults = source.ContentPreview.IncludeRejectedResults;
        return snapshot;
    }
}

namespace Runiq.Rag.Models.Retrieval;

/// <summary>
/// Represents a provider-independent metadata filter that a retrieval request can carry to narrow candidate
/// matches during query-time retrieval. The filter intentionally exposes only exact-match equality
/// constraints expressed as string key/value pairs so it never leaks provider-specific query syntax. It is
/// designed as a minimal, forward-extensible contract: richer constraint kinds (ranges, negation, boolean
/// groups) can be layered on later without changing how existing equality filters are expressed.
/// </summary>
public sealed class RetrievalMetadataFilter
{
    /// <summary>
    /// Initializes a new empty instance of the <see cref="RetrievalMetadataFilter"/> class that applies no
    /// constraints and therefore matches every candidate.
    /// </summary>
    public RetrievalMetadataFilter()
    {
        EqualityFilters = new Dictionary<string, string>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RetrievalMetadataFilter"/> class by copying the provided
    /// exact-match equality constraints so later mutation of the source dictionary cannot alter the filter.
    /// </summary>
    /// <param name="equalityFilters">
    /// The metadata field names and the exact values each field must equal for a candidate to match.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="equalityFilters"/> is null.</exception>
    public RetrievalMetadataFilter(IReadOnlyDictionary<string, string> equalityFilters)
    {
        ArgumentNullException.ThrowIfNull(equalityFilters);

        EqualityFilters = new Dictionary<string, string>(equalityFilters);
    }

    /// <summary>
    /// Gets an empty filter that applies no constraints and matches every candidate.
    /// </summary>
    public static RetrievalMetadataFilter Empty { get; } = new();

    /// <summary>
    /// Gets the exact-match equality constraints. Each entry requires the named metadata field to equal the
    /// associated value, and all entries must match for a candidate to be retained (logical AND).
    /// </summary>
    public IReadOnlyDictionary<string, string> EqualityFilters { get; }

    /// <summary>
    /// Gets a value indicating whether the filter applies no constraints, in which case retrieval should not
    /// exclude any candidate on the basis of this filter.
    /// </summary>
    public bool IsEmpty => EqualityFilters.Count == 0;
}

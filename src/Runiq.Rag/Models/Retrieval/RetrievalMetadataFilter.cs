namespace Runiq.Rag.Models.Retrieval;

/// <summary>
/// Represents a provider-independent metadata filter that a retrieval query can carry to narrow candidate
/// matches before similarity scoring. The filter is a list of <see cref="RetrievalMetadataFilterCriterion"/>
/// entries combined with logical AND semantics: a candidate record must satisfy every criterion to be
/// retained. It intentionally never leaks provider-specific query syntax (SQL, query DSLs, raw expression
/// strings); each vector store translates the criteria into its own native filtering mechanism. Only
/// exact-match equality is currently defined, and richer operators can be added later through
/// <see cref="RetrievalMetadataFilterOperator"/> without reshaping existing filters.
/// </summary>
public sealed class RetrievalMetadataFilter
{
    /// <summary>
    /// Initializes a new empty instance of the <see cref="RetrievalMetadataFilter"/> class that applies no
    /// constraints and therefore matches every candidate.
    /// </summary>
    public RetrievalMetadataFilter()
    {
        Criteria = [];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RetrievalMetadataFilter"/> class from the provided
    /// criteria. The criteria are copied so later mutation of the source collection cannot alter the filter.
    /// </summary>
    /// <param name="criteria">The metadata criteria that a candidate must all satisfy (logical AND).</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="criteria"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="criteria"/> contains a null entry.</exception>
    public RetrievalMetadataFilter(IEnumerable<RetrievalMetadataFilterCriterion> criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        var copiedCriteria = criteria.ToList();

        if (copiedCriteria.Any(criterion => criterion is null))
        {
            throw new ArgumentException("Metadata filter criteria cannot contain null entries.", nameof(criteria));
        }

        Criteria = copiedCriteria;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RetrievalMetadataFilter"/> class from exact-match
    /// key/value pairs, mapping each pair to an equality criterion. The pairs are copied so later mutation of
    /// the source collection cannot alter the filter.
    /// </summary>
    /// <param name="equalityFilters">
    /// The metadata field names and the exact values each field must equal for a candidate to match.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="equalityFilters"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a pair carries a null, empty, or whitespace key.</exception>
    public RetrievalMetadataFilter(IEnumerable<KeyValuePair<string, string>> equalityFilters)
    {
        ArgumentNullException.ThrowIfNull(equalityFilters);

        Criteria = equalityFilters
            .Select(pair => new RetrievalMetadataFilterCriterion(pair.Key, pair.Value))
            .ToList();
    }

    /// <summary>
    /// Gets an empty filter that applies no constraints and matches every candidate.
    /// </summary>
    public static RetrievalMetadataFilter Empty { get; } = new();

    /// <summary>
    /// Gets the metadata criteria. All criteria must match for a candidate to be retained (logical AND); an
    /// empty collection applies no constraints.
    /// </summary>
    public IReadOnlyList<RetrievalMetadataFilterCriterion> Criteria { get; }

    /// <summary>
    /// Gets a value indicating whether the filter applies no constraints, in which case retrieval should not
    /// exclude any candidate on the basis of this filter.
    /// </summary>
    public bool IsEmpty => Criteria.Count == 0;
}

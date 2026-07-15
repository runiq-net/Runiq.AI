namespace Runiq.AI.Rag.Models.Retrieval;

/// <summary>
/// Represents a single provider-independent metadata constraint evaluated during query-time retrieval: a
/// metadata key, the expected value, and the comparison operator to apply. Criteria are combined by
/// <see cref="RetrievalMetadataFilter"/> with logical AND semantics, so a candidate record must satisfy every
/// criterion to be retained. The criterion carries no provider-specific query syntax; each vector store
/// translates it into its own native filtering mechanism.
/// </summary>
public sealed class RetrievalMetadataFilterCriterion
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RetrievalMetadataFilterCriterion"/> class. Invalid keys and
    /// null expected values are rejected at construction so a filter can never carry a structurally invalid
    /// criterion. The operator is intentionally not validated here: which operators are supported is a
    /// per-store decision reported deterministically by the store at query time.
    /// </summary>
    /// <param name="key">The metadata field name the criterion constrains.</param>
    /// <param name="value">The expected metadata value the record must carry for the key.</param>
    /// <param name="operator">The comparison operator to apply; defaults to exact-match equality.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
    public RetrievalMetadataFilterCriterion(
        string key,
        string value,
        RetrievalMetadataFilterOperator @operator = RetrievalMetadataFilterOperator.Equal)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Metadata filter criterion key is required.", nameof(key));
        }

        ArgumentNullException.ThrowIfNull(value);

        Key = key;
        Value = value;
        Operator = @operator;
    }

    /// <summary>
    /// Gets the metadata field name the criterion constrains. Keys are compared against record metadata keys
    /// deterministically using ordinal (case-sensitive) semantics; a record without this key never matches.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Gets the expected metadata value. For <see cref="RetrievalMetadataFilterOperator.Equal"/>, the record's
    /// stored value must be ordinally equal to this value for the criterion to match.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Gets the comparison operator applied between the record's metadata value and <see cref="Value"/>.
    /// Only equality is currently defined; the enum leaves room for future operators.
    /// </summary>
    public RetrievalMetadataFilterOperator Operator { get; }
}


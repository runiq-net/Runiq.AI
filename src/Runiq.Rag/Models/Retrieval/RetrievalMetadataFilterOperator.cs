namespace Runiq.Rag.Models.Retrieval;

/// <summary>
/// Defines the provider-independent comparison operators a metadata filter criterion can apply during
/// query-time retrieval. The enumeration deliberately avoids provider-specific operator names or query
/// syntax so filters stay portable across vector store implementations, and it leaves room for additional
/// operators (ranges, negation, containment) to be introduced later without reshaping existing criteria.
/// </summary>
public enum RetrievalMetadataFilterOperator
{
    /// <summary>
    /// Requires the record's metadata value for the criterion key to be exactly equal to the expected value.
    /// String comparison is deterministic and case-sensitive (ordinal).
    /// </summary>
    Equal = 0,
}

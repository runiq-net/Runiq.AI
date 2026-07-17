namespace Runiq.AI.Agents;

/// <summary>
/// Represents a source citation that was referenced by the assistant and validated against the selected model context.
/// </summary>
public sealed record AgentCitation
{
    /// <summary>Initializes validated, content-free citation metadata.</summary>
    /// <param name="number">The one-based citation number used in the assistant response.</param>
    /// <param name="documentId">The selected document identifier.</param>
    /// <param name="chunkId">The selected chunk identifier.</param>
    /// <param name="retrievalCorrelationId">The retrieval execution that selected the source.</param>
    /// <param name="contextOrder">The zero-based order of the source in model context.</param>
    /// <param name="markerCount">The number of validated marker occurrences in the response.</param>
    /// <param name="rawScore">The finite raw retrieval score, when available.</param>
    /// <param name="normalizedRelevance">The normalized retrieval relevance, when available.</param>
    /// <param name="metric">The retrieval score metric, when available.</param>
    /// <param name="higherIsBetter">Whether higher raw scores rank first, when known.</param>
    public AgentCitation(
        int number,
        string documentId,
        string chunkId,
        string retrievalCorrelationId,
        int contextOrder,
        int markerCount,
        double? rawScore = null,
        double? normalizedRelevance = null,
        string? metric = null,
        bool? higherIsBetter = null)
    {
        if (number <= 0) throw new ArgumentOutOfRangeException(nameof(number), "Citation number must be positive.");
        if (contextOrder < 0) throw new ArgumentOutOfRangeException(nameof(contextOrder), "Context order cannot be negative.");
        if (number != contextOrder + 1) throw new ArgumentException("Citation number must equal context order plus one.", nameof(number));
        if (markerCount <= 0) throw new ArgumentOutOfRangeException(nameof(markerCount), "Marker count must be positive.");
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(chunkId);
        ArgumentException.ThrowIfNullOrWhiteSpace(retrievalCorrelationId);
        if (rawScore is double raw && !double.IsFinite(raw)) throw new ArgumentOutOfRangeException(nameof(rawScore), "Raw score must be finite.");
        if (normalizedRelevance is double relevance && (!double.IsFinite(relevance) || relevance is < 0 or > 1))
            throw new ArgumentOutOfRangeException(nameof(normalizedRelevance), "Normalized relevance must be finite and between zero and one.");
        if (higherIsBetter.HasValue && string.IsNullOrWhiteSpace(metric))
            throw new ArgumentException("Score direction requires a metric.", nameof(higherIsBetter));
        if (metric is not null && string.IsNullOrWhiteSpace(metric)) throw new ArgumentException("Metric cannot be whitespace.", nameof(metric));

        Number = number;
        DocumentId = documentId.Trim();
        ChunkId = chunkId.Trim();
        RetrievalCorrelationId = retrievalCorrelationId.Trim();
        ContextOrder = contextOrder;
        MarkerCount = markerCount;
        RawScore = rawScore;
        NormalizedRelevance = normalizedRelevance;
        Metric = metric?.Trim();
        HigherIsBetter = higherIsBetter;
    }

    /// <summary>Gets the one-based citation number used in the assistant response.</summary>
    public int Number { get; }
    /// <summary>Gets the selected document identifier.</summary>
    public string DocumentId { get; }
    /// <summary>Gets the selected chunk identifier.</summary>
    public string ChunkId { get; }
    /// <summary>Gets the correlation identifier of the retrieval that selected the source.</summary>
    public string RetrievalCorrelationId { get; }
    /// <summary>Gets the zero-based source order in model context.</summary>
    public int ContextOrder { get; }
    /// <summary>Gets the number of validated marker occurrences in the response.</summary>
    public int MarkerCount { get; }
    /// <summary>Gets the finite raw retrieval score, when available.</summary>
    public double? RawScore { get; }
    /// <summary>Gets normalized retrieval relevance, when available.</summary>
    public double? NormalizedRelevance { get; }
    /// <summary>Gets the retrieval score metric, when available.</summary>
    public string? Metric { get; }
    /// <summary>Gets whether higher raw scores rank first, when known.</summary>
    public bool? HigherIsBetter { get; }
}

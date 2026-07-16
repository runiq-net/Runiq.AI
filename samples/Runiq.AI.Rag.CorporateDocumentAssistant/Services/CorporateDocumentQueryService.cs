using Microsoft.Extensions.Options;
using Runiq.AI.Rag.Abstractions.Retrieval;
using Runiq.AI.Rag.CorporateDocumentAssistant.Models;
using Runiq.AI.Rag.Models.Retrieval;

namespace Runiq.AI.Rag.CorporateDocumentAssistant.Services;

/// <summary>
/// Runs query-time retrieval and composes a deterministic sample answer from retrieved corporate document chunks.
/// </summary>
public sealed class CorporateDocumentQueryService
{
    private readonly IRagRetrievalPipeline retrievalPipeline;
    private readonly CorporateDocumentAssistantOptions options;

    /// <summary>
    /// Initializes a new instance of the <see cref="CorporateDocumentQueryService"/> class.
    /// </summary>
    /// <param name="retrievalPipeline">The provider-independent RAG retrieval pipeline.</param>
    /// <param name="options">The sample options that provide the target vector index name.</param>
    public CorporateDocumentQueryService(
        IRagRetrievalPipeline retrievalPipeline,
        IOptions<CorporateDocumentAssistantOptions> options)
    {
        this.retrievalPipeline = retrievalPipeline ?? throw new ArgumentNullException(nameof(retrievalPipeline));
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Retrieves relevant chunks for a question and returns a deterministic answer with visible source chunks.
    /// </summary>
    /// <param name="request">The user question and retrieval settings.</param>
    /// <param name="cancellationToken">A token that can cancel retrieval.</param>
    /// <returns>The answer and source chunks selected by retrieval.</returns>
    public async Task<CorporateDocumentQueryResponse> AskAsync(
        CorporateDocumentQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Question))
        {
            throw new ArgumentException("Question is required.", nameof(request));
        }

        var topK = request.TopK <= 0 ? 4 : request.TopK;
        var question = request.Question.Trim();
        var retrievalResult = await retrievalPipeline.RetrieveAsync(
            new RetrievalRequest
            {
                IndexName = options.IndexName,
                QueryText = question,
                TopK = topK,
            },
            cancellationToken).ConfigureAwait(false);

        if (!retrievalResult.Succeeded)
        {
            throw new InvalidOperationException($"Retrieval failed: {retrievalResult.Reason}");
        }

        var sources = retrievalResult.Items
            .Select(MapSource)
            .ToArray();

        return new CorporateDocumentQueryResponse
        {
            Question = question,
            IndexName = options.IndexName,
            Answer = ComposeAnswer(question, sources),
            Sources = sources,
        };
    }

    private static CorporateDocumentSourceChunk MapSource(RetrievalResultItem item)
    {
        var metadata = item.Metadata.Values;

        return new CorporateDocumentSourceChunk
        {
            ChunkId = GetMetadata(metadata, "chunkId", item.RecordId),
            DocumentId = GetMetadata(metadata, "documentId", "unknown-document"),
            SourceName = GetMetadata(metadata, "sourceName", "Unknown source"),
            ChunkIndex = GetMetadata(metadata, "chunkIndex", "n/a"),
            RawScore = item.RawScore,
            Relevance = item.Relevance,
            Metric = item.Metric,
            Snippet = CreateSnippet(item.Content),
        };
    }

    private static string ComposeAnswer(
        string question,
        IReadOnlyList<CorporateDocumentSourceChunk> sources)
    {
        if (sources.Count == 0)
        {
            return $"No matching corporate document chunks were found for: {question}";
        }

        var sourceNames = sources
            .Select(source => source.SourceName)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        return "Demo answer based on retrieved corporate document context. " +
            $"Review the cited source chunks from {string.Join(", ", sourceNames)} for the grounded details.";
    }

    private static string CreateSnippet(string content)
    {
        const int maxLength = 320;

        var snippet = content
            .ReplaceLineEndings(" ")
            .Trim();

        return snippet.Length <= maxLength
            ? snippet
            : string.Concat(snippet.AsSpan(0, maxLength), "...");
    }

    private static string GetMetadata(
        IDictionary<string, string> metadata,
        string key,
        string fallback)
    {
        return metadata.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;
    }
}


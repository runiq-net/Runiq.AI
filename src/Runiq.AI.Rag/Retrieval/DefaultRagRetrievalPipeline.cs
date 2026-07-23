using Microsoft.Extensions.Options;
using Runiq.AI.Core.AI.Embeddings;
using Runiq.AI.Core.Configuration;
using Runiq.AI.Core.Models;
using Runiq.AI.Rag.Abstractions.Retrieval;
using Runiq.AI.Rag.Configuration;
using Runiq.AI.Rag.Abstractions.Embeddings;
using Runiq.AI.Rag.Embeddings;
using Runiq.AI.Rag.Abstractions.Telemetry;
using Runiq.AI.Rag.Abstractions.VectorStores;
using Runiq.AI.Rag.Models.Retrieval;
using Runiq.AI.Rag.Models.Search;
using Runiq.AI.Rag.Models.VectorStores;

namespace Runiq.AI.Rag.Retrieval;

/// <summary>
/// Runs the default query-time retrieval pipeline by combining <see cref="IEmbeddingClient"/> and
/// <see cref="IRagVectorStore"/>. This is an orchestration layer only: it embeds the query text through the
/// embedding abstraction, forwards the resulting query vector together with the request's index name, top-k
/// value, and metadata filter to the vector store query operation, and maps the store's matches into the
/// standard <see cref="RetrievalResult"/>. It contains no provider-specific logic and computes no score
/// normalization of its own; raw score semantics and relevance are taken from the vector store query result.
/// </summary>
/// <remarks>
/// Error handling boundary: only an already-cancelled <see cref="CancellationToken"/> is surfaced as an
/// exception, matching the cancellation standard of the other RAG pipelines. Every other failure — a null
/// request, a missing index name, a non-positive top-k value, a request without retrievable query input, an
/// embedding abstraction that throws or produces an empty embedding, and a vector store query that throws or
/// reports an unsuccessful result — is normalized into a failed <see cref="RetrievalResult"/> with a
/// deterministic <see cref="RetrievalErrorCode"/> so that no provider-specific exception type, message, or
/// SDK detail crosses this pipeline's public contract. When request validation or embedding fails, the vector
/// store is never queried, and when request validation fails the embedding abstraction is never invoked
/// either.
/// </remarks>
public sealed class DefaultRagRetrievalPipeline : IRagRetrievalPipeline
{
    private const string NullRequestReason = "Retrieval request is required.";
    private const string MissingIndexNameReason = "Retrieval request index name must be a non-empty, non-whitespace value.";
    private const string InvalidTopKReason = "Retrieval request TopK must be greater than zero.";
    private const string InvalidModeReason = "Retrieval request mode must be a defined retrieval mode.";
    private const string MissingQueryReason = "Retrieval request must carry non-empty query text or a non-empty query vector.";
    private const string EmbeddingFailedReason = "Query embedding generation failed.";
    private const string VectorStoreQueryFailedReason = "Vector store query failed.";

    /// <summary>The default RRF constant used by hybrid retrieval.</summary>
    public const int DefaultReciprocalRankFusionConstant = 60;

    private readonly IEmbeddingClient? embeddingClient;
    private readonly IRagVectorStore vectorStore;
    private readonly IRagOperationTelemetryRecorder? telemetryRecorder;
    private readonly TimeProvider timeProvider;
    private readonly RagOptions options;
    private readonly IRagIndexRegistry? indexRegistry;
    private readonly IRagIndexRuntimeConfigurationResolver? runtimeResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultRagRetrievalPipeline"/> class.
    /// </summary>
    /// <param name="embeddingClient">The Core embedding client used to embed query text.</param>
    /// <param name="vectorStore">The provider-independent vector store contract used to run the similarity query.</param>
    /// <param name="telemetryRecorder">
    /// The optional, strictly observational telemetry recorder that receives each retrieval result and its
    /// measured duration. Null disables recording without changing retrieval behavior.
    /// </param>
    /// <param name="timeProvider">
    /// The time source used only to measure retrieval duration for telemetry. Null falls back to
    /// <see cref="TimeProvider.System"/>.
    /// </param>
    /// <param name="options">The RAG options used to resolve the embedding model.</param>
    /// <param name="indexRegistry">The optional registered-index source of truth.</param>
    /// <param name="runtimeResolver">The optional per-index runtime dependency resolver.</param>
    public DefaultRagRetrievalPipeline(
        IEmbeddingClient embeddingClient,
        IRagVectorStore vectorStore,
        IRagOperationTelemetryRecorder? telemetryRecorder = null,
        TimeProvider? timeProvider = null,
        IOptions<RagOptions>? options = null,
        IRagIndexRegistry? indexRegistry = null,
        IRagIndexRuntimeConfigurationResolver? runtimeResolver = null)
    {
        this.embeddingClient = embeddingClient ?? throw new ArgumentNullException(nameof(embeddingClient));
        this.vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
        this.telemetryRecorder = telemetryRecorder;
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.options = options?.Value ?? new RagOptions();
        this.indexRegistry = indexRegistry;
        this.runtimeResolver = runtimeResolver;
    }

    /// <summary>
    /// Initializes a lexical-capable pipeline without requiring an embedding client registration.
    /// Semantic and hybrid requests fail explicitly when no per-index embedding client is available.
    /// </summary>
    /// <param name="vectorStore">The provider-independent store.</param>
    /// <param name="telemetryRecorder">The optional telemetry recorder.</param>
    /// <param name="timeProvider">The optional time source.</param>
    /// <param name="options">The optional RAG options.</param>
    /// <param name="indexRegistry">The optional index registry.</param>
    /// <param name="runtimeResolver">The optional per-index runtime resolver.</param>
    public DefaultRagRetrievalPipeline(
        IRagVectorStore vectorStore,
        IRagOperationTelemetryRecorder? telemetryRecorder = null,
        TimeProvider? timeProvider = null,
        IOptions<RagOptions>? options = null,
        IRagIndexRegistry? indexRegistry = null,
        IRagIndexRuntimeConfigurationResolver? runtimeResolver = null)
    {
        this.vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
        this.telemetryRecorder = telemetryRecorder;
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.options = options?.Value ?? new RagOptions();
        this.indexRegistry = indexRegistry;
        this.runtimeResolver = runtimeResolver;
    }

    /// <inheritdoc />
    public async Task<RetrievalResult> RetrieveAsync(
        RetrievalRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var startTimestamp = timeProvider.GetTimestamp();
        var result = await RetrieveCoreAsync(request, cancellationToken).ConfigureAwait(false);

        RecordRetrieval(result, timeProvider.GetElapsedTime(startTimestamp));

        return result;
    }

    /// <summary>
    /// Runs the retrieval pipeline exactly as documented on the class contract. Kept separate from
    /// <see cref="RetrieveAsync"/> so every result boundary flows through a single telemetry recording
    /// point without changing any result or error handling behavior. Cancellation surfaced as an
    /// exception bypasses recording because no result was produced.
    /// </summary>
    private async Task<RetrievalResult> RetrieveCoreAsync(
        RetrievalRequest request,
        CancellationToken cancellationToken)
    {
        var validationFailure = ValidateRequest(request);
        if (validationFailure is not null)
        {
            return validationFailure;
        }

        var runtime = ResolveRuntime(request.IndexName, request.Mode);
        if (request.Mode == RagRetrievalMode.Lexical)
        {
            return await RetrieveLexicalAsync(runtime.VectorStore, request, cancellationToken).ConfigureAwait(false);
        }

        if (runtime.EmbeddingClient is null && request.QueryVector is not { Count: > 0 })
        {
            return RetrievalResult.Failure(RetrievalErrorCode.EmbeddingFailed, EmbeddingFailedReason);
        }

        var queryVector = request.QueryVector is { Count: > 0 }
            ? request.QueryVector
            : await ResolveQueryVectorAsync(request.QueryText!, runtime.EmbeddingClient!, runtime.EmbeddingModel, cancellationToken).ConfigureAwait(false);
        if (queryVector is null || queryVector.Count == 0)
            return RetrievalResult.Failure(RetrievalErrorCode.EmbeddingFailed, EmbeddingFailedReason);

        RetrievalResult semantic;
        try
        {
            var queryResult = await runtime.VectorStore.QueryAsync(
                new QueryVectorRequest
                {
                    IndexName = request.IndexName,
                    Values = queryVector,
                    TopK = request.TopK,
                    MetadataFilter = request.MetadataFilter,
                },
                cancellationToken).ConfigureAwait(false);
            semantic = queryResult is null || !queryResult.Succeeded
                ? RetrievalResult.Failure(RetrievalErrorCode.VectorStoreQueryFailed, VectorStoreQueryFailedReason)
                : RetrievalResult.Success(MapItems(queryResult, RagRetrievalMode.Semantic), queryResult.Metadata,
                    new RagRetrievalStatistics
                    {
                        SemanticCandidateCount = queryResult.Records.Count,
                        LexicalCandidateCount = 0,
                        FusedCandidateCount = 0,
                    });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return RetrievalResult.Failure(RetrievalErrorCode.VectorStoreQueryFailed, VectorStoreQueryFailedReason);
        }
        if (!semantic.Succeeded || request.Mode == RagRetrievalMode.Semantic) return semantic;

        var lexical = await RetrieveLexicalAsync(runtime.VectorStore, request, cancellationToken).ConfigureAwait(false);
        if (!lexical.Succeeded) return lexical;
        var fused = Fuse(semantic.Items, lexical.Items);
        return RetrievalResult.Success(fused.Take(request.TopK).ToArray(), statistics: new RagRetrievalStatistics
        {
            SemanticCandidateCount = semantic.Items.Count,
            LexicalCandidateCount = lexical.Items.Count,
            FusedCandidateCount = fused.Count,
        });
    }

    /// <summary>
    /// Forwards the retrieval result and measured duration to the optional telemetry recorder. Recording is
    /// strictly observational: when no recorder is configured nothing happens, and a recorder that throws is
    /// ignored so that telemetry can never alter the retrieval result or error handling contract.
    /// </summary>
    private void RecordRetrieval(RetrievalResult result, TimeSpan duration)
    {
        if (telemetryRecorder is null)
        {
            return;
        }

        try
        {
            telemetryRecorder.RecordRetrieval(result, duration);
        }
        catch
        {
            // Telemetry must never change retrieval behavior; recorder failures are intentionally ignored.
        }
    }

    /// <summary>
    /// Validates the retrieval request deterministically before any provider abstraction is invoked. Returns a
    /// failed result with <see cref="RetrievalErrorCode.InvalidRequest"/> when the request is null, targets no
    /// index, carries a non-positive top-k value, or carries no retrievable query input; returns null when the
    /// request is valid.
    /// </summary>
    private static RetrievalResult? ValidateRequest(RetrievalRequest? request)
    {
        if (request is null)
        {
            return RetrievalResult.Failure(RetrievalErrorCode.InvalidRequest, NullRequestReason);
        }

        if (string.IsNullOrWhiteSpace(request.IndexName))
        {
            return RetrievalResult.Failure(RetrievalErrorCode.InvalidRequest, MissingIndexNameReason);
        }

        if (request.TopK <= 0)
        {
            return RetrievalResult.Failure(RetrievalErrorCode.InvalidRequest, InvalidTopKReason);
        }

        if (!Enum.IsDefined(request.Mode))
        {
            return RetrievalResult.Failure(RetrievalErrorCode.InvalidRequest, InvalidModeReason);
        }

        if (!request.HasRetrievableQuery || request.Mode == RagRetrievalMode.Lexical && string.IsNullOrWhiteSpace(request.QueryText))
        {
            return RetrievalResult.Failure(RetrievalErrorCode.InvalidRequest, MissingQueryReason);
        }

        return null;
    }

    /// <summary>
    /// Embeds the query text through the embedding abstraction, normalizing every non-cancellation failure to
    /// a null vector so the caller can report <see cref="RetrievalErrorCode.EmbeddingFailed"/> without leaking
    /// provider-specific exception details.
    /// </summary>
    private async Task<IReadOnlyList<float>?> ResolveQueryVectorAsync(
        string queryText,
        IEmbeddingClient effectiveEmbeddingClient,
        ModelReference effectiveModel,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await effectiveEmbeddingClient.EmbedAsync(new EmbeddingRequest(effectiveModel, [queryText], Dimensions: effectiveModel.EmbeddingDimensions), cancellationToken).ConfigureAwait(false);
            return response.Results.SingleOrDefault()?.Vector;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return null;
        }
    }

    private ModelReference ResolveEmbeddingModel()
    {
        if (string.IsNullOrWhiteSpace(options.EmbeddingModel)) return ModelReference.Parse("openai/rag-embedding");

        return ProviderModelReferenceResolver.Resolve(ModelReference.Parse(options.EmbeddingModel), options.EmbeddingProvider);
    }

    private (IEmbeddingClient? EmbeddingClient, ModelReference EmbeddingModel, IRagVectorStore VectorStore) ResolveRuntime(
        string indexName, RagRetrievalMode mode)
    {
        if (runtimeResolver is not null && indexRegistry?.Registrations.Any(index => string.Equals(index.Name, indexName, StringComparison.Ordinal)) == true)
        {
            if (mode == RagRetrievalMode.Lexical)
            {
                return (null, ResolveEmbeddingModel(), runtimeResolver.ResolveVectorStore(indexName));
            }
            var runtime = runtimeResolver.Resolve(indexName);
            return (runtime.EmbeddingClient, runtime.EmbeddingModel, runtime.VectorStore);
        }
        return (embeddingClient, ResolveEmbeddingModel(), vectorStore);
    }

    /// <summary>
    /// Maps the vector store's matches into standard retrieval result items, carrying over the record id,
    /// stored chunk content, metadata, raw score, metric direction, and normalized relevance reported by the store.
    /// </summary>
    private static IReadOnlyList<RetrievalResultItem> MapItems(QueryVectorResult queryResult, RagRetrievalMode mode)
    {
        return queryResult.Records
            .Select((record, rank) => new RetrievalResultItem
            {
                RecordId = record.Id,
                Content = record.Content,
                Metadata = record.Metadata,
                RawScore = record.RawScore,
                Relevance = record.Relevance,
                Metric = record.Metric,
                HigherIsBetter = record.HigherIsBetter,
                Provenance = new RagRetrievalProvenance
                {
                    Mode = mode,
                    SemanticRank = mode == RagRetrievalMode.Semantic ? rank + 1 : null,
                    SemanticRawScore = mode == RagRetrievalMode.Semantic ? record.RawScore : null,
                },
            })
            .ToList();
    }

    private static async Task<RetrievalResult> RetrieveLexicalAsync(
        IRagVectorStore store, RetrievalRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await store.QueryLexicalAsync(new QueryLexicalRequest
            {
                IndexName = request.IndexName,
                QueryText = request.QueryText!,
                TopK = request.TopK,
                MetadataFilter = request.MetadataFilter,
            }, cancellationToken).ConfigureAwait(false);
            if (result is null || !result.Succeeded)
                return RetrievalResult.Failure(RetrievalErrorCode.VectorStoreQueryFailed, "Lexical store query failed.");
            return RetrievalResult.Success(result.Records.Select((record, rank) => new RetrievalResultItem
            {
                RecordId = record.Id,
                Content = record.Content,
                Metadata = record.Metadata,
                RawScore = null,
                Relevance = null,
                Metric = null,
                HigherIsBetter = null,
                Provenance = new RagRetrievalProvenance
                {
                    Mode = RagRetrievalMode.Lexical,
                    LexicalRank = rank + 1,
                    LexicalRawScore = record.RawScore,
                },
            }).ToArray(), result.Metadata, new RagRetrievalStatistics
            {
                SemanticCandidateCount = 0,
                LexicalCandidateCount = result.Records.Count,
                FusedCandidateCount = 0,
            });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return RetrievalResult.Failure(RetrievalErrorCode.VectorStoreQueryFailed, "Lexical store query failed.");
        }
    }

    private static IReadOnlyList<RetrievalResultItem> Fuse(
        IReadOnlyList<RetrievalResultItem> semantic,
        IReadOnlyList<RetrievalResultItem> lexical)
    {
        var candidates = new Dictionary<(string DocumentId, string ChunkId), FusionCandidate>();
        Add(semantic, semanticSource: true);
        Add(lexical, semanticSource: false);
        var ordered = candidates.Values
            .OrderByDescending(item => item.Score)
            .ThenBy(item => Math.Min(item.SemanticRank ?? int.MaxValue, item.LexicalRank ?? int.MaxValue))
            .ThenBy(item => item.SemanticRank ?? int.MaxValue)
            .ThenBy(item => item.LexicalRank ?? int.MaxValue)
            .ThenBy(item => item.DocumentId, StringComparer.Ordinal)
            .ThenBy(item => item.Item.RecordId, StringComparer.Ordinal)
            .ToArray();
        return ordered.Select((candidate, rank) => new RetrievalResultItem
        {
            RecordId = candidate.Item.RecordId,
            Content = candidate.Item.Content,
            Metadata = candidate.Item.Metadata,
            RawScore = candidate.Item.RawScore,
            Relevance = candidate.Item.Relevance,
            Metric = candidate.Item.Metric,
            HigherIsBetter = candidate.Item.HigherIsBetter,
            Provenance = new RagRetrievalProvenance
            {
                Mode = RagRetrievalMode.Hybrid,
                SemanticRank = candidate.SemanticRank,
                LexicalRank = candidate.LexicalRank,
                SemanticRawScore = candidate.SemanticRank is not null
                    ? candidate.Item.Provenance?.SemanticRawScore
                    : null,
                LexicalRawScore = candidate.LexicalRank is not null
                    ? lexical[candidate.LexicalRank.Value - 1].Provenance?.LexicalRawScore
                    : null,
                ReciprocalRankFusionScore = candidate.Score,
                FusedRank = rank + 1,
            },
        }).ToArray();

        void Add(IReadOnlyList<RetrievalResultItem> source, bool semanticSource)
        {
            for (var index = 0; index < source.Count; index++)
            {
                var item = source[index];
                var documentId = item.Metadata.Values.TryGetValue("documentId", out var value) ? value : string.Empty;
                var key = (documentId, item.RecordId);
                if (!candidates.TryGetValue(key, out var candidate))
                    candidate = new FusionCandidate(item, documentId);
                if (semanticSource) candidate.SemanticRank = index + 1;
                else candidate.LexicalRank = index + 1;
                candidates[key] = candidate;
            }
        }
    }

    private sealed class FusionCandidate(RetrievalResultItem item, string documentId)
    {
        public RetrievalResultItem Item { get; } = item;
        public string DocumentId { get; } = documentId;
        public int? SemanticRank { get; set; }
        public int? LexicalRank { get; set; }
        public double Score => (SemanticRank is int semantic ? 1d / (DefaultReciprocalRankFusionConstant + semantic) : 0d) +
            (LexicalRank is int lexical ? 1d / (DefaultReciprocalRankFusionConstant + lexical) : 0d);
    }
}


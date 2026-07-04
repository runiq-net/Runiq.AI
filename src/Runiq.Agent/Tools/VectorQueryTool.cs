using Runiq.Rag.Abstractions.Tools;
using Runiq.Rag.Models.Metadata;
using Runiq.Rag.Models.Retrieval;
using Runiq.Rag.Models.Tools;

namespace Runiq.Agents.Tools;

/// <summary>
/// Agent-facing <see cref="IRuniqTool{TInput, TOutput}"/> adapter that lets the existing agent tool runtime
/// (registration, <see cref="AgentToolInvoker"/>) invoke retrieval. It is the bridge where RAG meets the agent
/// runtime: it maps a <see cref="VectorQueryToolInput"/> supplied by the model to a
/// <see cref="VectorQueryToolRequest"/>, delegates execution to the existing provider-independent
/// <see cref="IVectorQueryTool"/>, and maps the resulting <see cref="VectorQueryToolResult"/> back to an
/// agent-usable <see cref="VectorQueryToolOutput"/>. The adapter introduces no new RAG concept, retrieval
/// pipeline, or provider routing; it delegates all retrieval semantics to <see cref="IVectorQueryTool"/> and the
/// pipeline behind it.
/// </summary>
[RuniqTool(
    name: "vector_query",
    description: "Runs a retrieval query against a configured vector store and index, returning the stored chunks that best match the supplied query text.")]
public sealed class VectorQueryTool : IRuniqTool<VectorQueryToolInput, VectorQueryToolOutput>
{
    private const string NullInputReason = "Vector query tool input is required.";

    private readonly IVectorQueryTool vectorQueryTool;

    /// <summary>
    /// Initializes a new instance of the <see cref="VectorQueryTool"/> class.
    /// </summary>
    /// <param name="vectorQueryTool">The existing provider-independent Vector Query Tool the adapter delegates to.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="vectorQueryTool"/> is null.</exception>
    public VectorQueryTool(IVectorQueryTool vectorQueryTool)
    {
        this.vectorQueryTool = vectorQueryTool ?? throw new ArgumentNullException(nameof(vectorQueryTool));
    }

    /// <summary>
    /// Executes the tool by adapting the agent-supplied <paramref name="input"/> to a
    /// <see cref="VectorQueryToolRequest"/>, delegating to <see cref="IVectorQueryTool"/>, and mapping the
    /// outcome back to a <see cref="VectorQueryToolOutput"/>. Only the adapter-boundary condition the delegated
    /// tool cannot see — a null input coming from the runtime — is handled here as a deterministic failed
    /// output; every other validation (missing vector store name, missing index name, empty query, non-positive
    /// top-k, embedding or vector store failures) is left to the delegated tool and the retrieval pipeline, which
    /// report them as failed results rather than exceptions.
    /// </summary>
    /// <param name="input">The agent-facing Vector Query Tool input.</param>
    /// <param name="cancellationToken">
    /// A token that cancels the operation. It is forwarded through the delegation so the retrieval call chain can
    /// observe cancellation.
    /// </param>
    /// <returns>
    /// A successful output carrying the retrieved matches, or a failed output carrying a provider-independent
    /// error code. Failures from the delegated tool are returned as failed output, not thrown.
    /// </returns>
    public async Task<VectorQueryToolOutput> ExecuteAsync(
        VectorQueryToolInput input,
        CancellationToken cancellationToken = default)
    {
        if (input is null)
        {
            return VectorQueryToolOutput.Failure(RetrievalErrorCode.InvalidRequest, NullInputReason);
        }

        var request = new VectorQueryToolRequest
        {
            VectorStoreName = input.VectorStoreName,
            IndexName = input.IndexName,
            QueryText = input.QueryText,
            EmbeddingModel = input.EmbeddingModel,
            TopK = input.TopK,
            MetadataFilter = input.MetadataFilter ?? RetrievalMetadataFilter.Empty,
        };

        var result = await vectorQueryTool
            .ExecuteAsync(request, cancellationToken)
            .ConfigureAwait(false);

        return VectorQueryToolOutput.FromResult(result);
    }
}

/// <summary>
/// Agent-facing input the model supplies when invoking the <see cref="VectorQueryTool"/>. It is a thin,
/// JSON-serializable carrier whose members are init-only so the agent tool runtime's System.Text.Json (Web
/// defaults) deserialization can populate it. Semantic validation is not performed here; values are forwarded to
/// the delegated <see cref="IVectorQueryTool"/>, which reports invalid values deterministically as failed
/// results.
/// </summary>
public sealed record VectorQueryToolInput
{
    /// <summary>
    /// Gets the name that associates the invocation with a configured vector store. This is an
    /// association/configuration value only; it does not select or route to a concrete vector store provider at
    /// the adapter level.
    /// </summary>
    public string VectorStoreName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the target vector index the query should run against.
    /// </summary>
    public string IndexName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the natural-language query text the agent wants to retrieve against.
    /// </summary>
    public string QueryText { get; init; } = string.Empty;

    /// <summary>
    /// Gets the optional identifier of the embedding model to use when turning <see cref="QueryText"/> into a
    /// query vector. This is a provider-independent association/configuration value only; it does not resolve or
    /// route to a concrete embedding provider at the adapter level. A null value lets the retrieval pipeline use
    /// its configured embedding model.
    /// </summary>
    public string? EmbeddingModel { get; init; }

    /// <summary>
    /// Gets the maximum number of matches to return, ordered best match first. Defaults to five, matching
    /// <see cref="VectorQueryToolRequest.TopK"/>.
    /// </summary>
    public int TopK { get; init; } = 5;

    /// <summary>
    /// Gets the optional provider-independent metadata filter forwarded to the retrieval flow to narrow candidate
    /// matches. A null value maps to <see cref="RetrievalMetadataFilter.Empty"/> ("no filtering") so both a
    /// supplied filter and an absent filter map cleanly onto <see cref="VectorQueryToolRequest.MetadataFilter"/>.
    /// </summary>
    public RetrievalMetadataFilter? MetadataFilter { get; init; }
}

/// <summary>
/// Agent-facing output produced by the <see cref="VectorQueryTool"/>. It reuses the existing retrieval contract
/// rather than introducing a parallel one: matches are exposed as <see cref="RetrievalResultItem"/> values and
/// failures reuse the existing <see cref="RetrievalErrorCode"/> categories. Members are init-only so the agent
/// tool runtime's System.Text.Json (Web defaults) serialization can emit the output.
/// </summary>
public sealed record VectorQueryToolOutput
{
    /// <summary>
    /// Gets a value indicating whether the invocation completed successfully. A successful invocation may still
    /// carry an empty <see cref="Matches"/> list when nothing matched the query.
    /// </summary>
    public bool Succeeded { get; init; }

    /// <summary>
    /// Gets the provider-independent error category, reused from the retrieval contract.
    /// <see cref="RetrievalErrorCode.None"/> for a successful invocation, including an empty-but-successful one.
    /// </summary>
    public RetrievalErrorCode ErrorCode { get; init; }

    /// <summary>
    /// Gets the provider-independent, human-readable failure reason. Empty for a successful invocation.
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Gets the retrieved matches ordered best match first, in the existing <see cref="RetrievalResultItem"/>
    /// shape. Empty for a failed invocation and for a successful invocation that matched nothing.
    /// </summary>
    public IReadOnlyList<RetrievalResultItem> Matches { get; init; } = Array.Empty<RetrievalResultItem>();

    /// <summary>
    /// Gets the provider-independent metadata describing the invocation outcome.
    /// </summary>
    public RagMetadata Metadata { get; init; } = RagMetadata.Empty;

    /// <summary>
    /// Maps a delegated <see cref="VectorQueryToolResult"/> to the agent-facing output, reusing its success flag,
    /// error code, reason, matches, and metadata unchanged.
    /// </summary>
    /// <param name="result">The result produced by the delegated <see cref="IVectorQueryTool"/>.</param>
    /// <returns>An agent-usable output projecting the delegated result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="result"/> is null.</exception>
    public static VectorQueryToolOutput FromResult(VectorQueryToolResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new VectorQueryToolOutput
        {
            Succeeded = result.Succeeded,
            ErrorCode = result.ErrorCode,
            Reason = result.Reason,
            Matches = result.Matches,
            Metadata = result.Metadata,
        };
    }

    /// <summary>
    /// Creates a failed output for an adapter-boundary condition the delegated tool cannot see (such as a null
    /// input), using a deterministic provider-independent error category.
    /// </summary>
    /// <param name="errorCode">The non-<see cref="RetrievalErrorCode.None"/> category describing the failure.</param>
    /// <param name="reason">A provider-independent, human-readable failure reason.</param>
    /// <returns>A failed output with an empty match list.</returns>
    public static VectorQueryToolOutput Failure(RetrievalErrorCode errorCode, string reason)
    {
        return new VectorQueryToolOutput
        {
            Succeeded = false,
            ErrorCode = errorCode,
            Reason = reason,
        };
    }
}

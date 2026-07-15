using System.Text.Json.Serialization;
using Runiq.AI.Rag.Abstractions.Tools;
using Runiq.AI.Rag.Models.Metadata;
using Runiq.AI.Rag.Models.Retrieval;
using Runiq.AI.Rag.Models.Tools;

namespace Runiq.AI.Agents.Tools;

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

        RetrievalMetadataFilter metadataFilter;
        try
        {
            // Map the JSON-deserializable filter DTO onto the RAG filter contract. A malformed criterion
            // (blank key or null entry) is an adapter-boundary condition the delegated tool never sees, so it
            // is surfaced as a deterministic failed output rather than thrown.
            metadataFilter = input.MetadataFilter?.ToRetrievalMetadataFilter() ?? RetrievalMetadataFilter.Empty;
        }
        catch (ArgumentException exception)
        {
            return VectorQueryToolOutput.Failure(RetrievalErrorCode.InvalidRequest, DescribeArgumentFailure(exception));
        }

        var request = new VectorQueryToolRequest
        {
            VectorStoreName = input.VectorStoreName,
            IndexName = input.IndexName,
            QueryText = input.QueryText,
            EmbeddingModel = input.EmbeddingModel,
            TopK = input.TopK,
            MetadataFilter = metadataFilter,
        };

        var result = await vectorQueryTool
            .ExecuteAsync(request, cancellationToken)
            .ConfigureAwait(false);

        return VectorQueryToolOutput.FromResult(result);
    }

    /// <summary>
    /// Produces an agent-facing failure reason from an <see cref="ArgumentException"/> raised while mapping the
    /// filter, dropping the trailing "(Parameter '...')" fragment that <see cref="ArgumentException.Message"/>
    /// appends so an internal parameter name is not leaked into agent-visible text.
    /// </summary>
    /// <param name="exception">The mapping failure to describe.</param>
    /// <returns>The failure message without the parameter-name suffix.</returns>
    private static string DescribeArgumentFailure(ArgumentException exception)
    {
        var reason = exception.Message;

        if (!string.IsNullOrEmpty(exception.ParamName))
        {
            var parameterSuffix = $" (Parameter '{exception.ParamName}')";
            if (reason.EndsWith(parameterSuffix, StringComparison.Ordinal))
            {
                reason = reason[..^parameterSuffix.Length];
            }
        }

        return reason;
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
    /// matches. This is a thin agent-facing DTO — not the RAG <see cref="RetrievalMetadataFilter"/> directly —
    /// because the RAG type's criteria are exposed through a read-only collection that System.Text.Json cannot
    /// populate; deserializing straight into it would silently drop the model-supplied criteria. The DTO is
    /// mapped to <see cref="RetrievalMetadataFilter"/> at execution time. A null value maps to
    /// <see cref="RetrievalMetadataFilter.Empty"/> ("no filtering") so both a supplied filter and an absent
    /// filter map cleanly onto <see cref="VectorQueryToolRequest.MetadataFilter"/>.
    /// </summary>
    public VectorQueryToolMetadataFilterInput? MetadataFilter { get; init; }
}

/// <summary>
/// Agent-facing, JSON-deserializable projection of a <see cref="RetrievalMetadataFilter"/> the model supplies to
/// narrow retrieval candidates. It exists because <see cref="RetrievalMetadataFilter.Criteria"/> is a read-only
/// collection that System.Text.Json (Web defaults) cannot populate during deserialization, so the model's
/// criteria would be silently dropped if the RAG type were used as the tool input directly. Its
/// <see cref="Criteria"/> member is init-only, letting the runtime deserializer assign the whole list, and it is
/// mapped back to the RAG contract via <see cref="ToRetrievalMetadataFilter"/>. It introduces no new filtering
/// concept; the mapping reuses <see cref="RetrievalMetadataFilterCriterion"/> and its validation.
/// </summary>
public sealed record VectorQueryToolMetadataFilterInput
{
    /// <summary>
    /// Gets the metadata criteria a candidate must all satisfy (logical AND). An empty or absent list applies no
    /// constraints and maps to <see cref="RetrievalMetadataFilter.Empty"/>.
    /// </summary>
    public IReadOnlyList<VectorQueryToolMetadataCriterionInput> Criteria { get; init; } =
        Array.Empty<VectorQueryToolMetadataCriterionInput>();

    /// <summary>
    /// Maps this DTO to the RAG <see cref="RetrievalMetadataFilter"/> contract, delegating per-criterion
    /// validation (non-blank key, non-null value) to <see cref="RetrievalMetadataFilterCriterion"/>. An absent,
    /// null, or empty criteria list all map to <see cref="RetrievalMetadataFilter.Empty"/> ("no filtering"); a
    /// null collection can occur when the model emits <c>"criteria": null</c>, which System.Text.Json binds as
    /// null over the default.
    /// </summary>
    /// <returns>The equivalent provider-independent <see cref="RetrievalMetadataFilter"/>.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when a supplied criterion is null or carries a blank key, so the adapter can surface it as a
    /// deterministic failed output rather than an unhandled exception.
    /// </exception>
    public RetrievalMetadataFilter ToRetrievalMetadataFilter()
    {
        if (Criteria is null || Criteria.Count == 0)
        {
            return RetrievalMetadataFilter.Empty;
        }

        var mappedCriteria = new List<RetrievalMetadataFilterCriterion>(Criteria.Count);
        foreach (var criterion in Criteria)
        {
            if (criterion is null)
            {
                throw new ArgumentException("Metadata filter criteria cannot contain null entries.", nameof(Criteria));
            }

            mappedCriteria.Add(criterion.ToRetrievalMetadataFilterCriterion());
        }

        return new RetrievalMetadataFilter(mappedCriteria);
    }
}

/// <summary>
/// Agent-facing, JSON-deserializable projection of a single <see cref="RetrievalMetadataFilterCriterion"/>. Its
/// members are init-only so the agent tool runtime's System.Text.Json (Web defaults) deserialization can populate
/// them; construction-time validation is deferred to <see cref="ToRetrievalMetadataFilterCriterion"/> so the
/// deserializer never throws while binding the input.
/// </summary>
public sealed record VectorQueryToolMetadataCriterionInput
{
    /// <summary>
    /// Gets the metadata field name the criterion constrains.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// Gets the expected metadata value the record must carry for the key.
    /// </summary>
    public string Value { get; init; } = string.Empty;

    /// <summary>
    /// Gets the comparison operator applied between the record's metadata value and <see cref="Value"/>. Reuses
    /// the RAG <see cref="RetrievalMetadataFilterOperator"/>; defaults to exact-match equality. The per-property
    /// string-enum converter lets the model supply the operator either by name (<c>"Equal"</c>) or numerically
    /// (<c>0</c>), so a name-based value is not rejected by the runtime's numeric-only Web defaults.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<RetrievalMetadataFilterOperator>))]
    public RetrievalMetadataFilterOperator Operator { get; init; } = RetrievalMetadataFilterOperator.Equal;

    /// <summary>
    /// Maps this DTO to the RAG <see cref="RetrievalMetadataFilterCriterion"/>, applying that type's validation
    /// (non-blank <see cref="Key"/>, non-null <see cref="Value"/>).
    /// </summary>
    /// <returns>The equivalent <see cref="RetrievalMetadataFilterCriterion"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when <see cref="Key"/> is blank.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <see cref="Value"/> is null.</exception>
    public RetrievalMetadataFilterCriterion ToRetrievalMetadataFilterCriterion()
    {
        return new RetrievalMetadataFilterCriterion(Key, Value, Operator);
    }
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


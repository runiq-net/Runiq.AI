using Runiq.AI.Core.AI.Embeddings;

namespace Runiq.AI.Rag.Tests.TestDoubles;

/// <summary>
/// Deterministic Core embedding client for RAG tests. It captures every request and preserves request input order.
/// </summary>
internal sealed class RecordingEmbeddingClient : IEmbeddingClient
{
    private readonly Func<EmbeddingRequest, EmbeddingResponse>? responseFactory;

    /// <summary>Initializes the client with deterministic vectors of the requested dimension.</summary>
    /// <param name="dimensions">The vector dimension count returned for each input.</param>
    /// <param name="responseFactory">An optional factory for tests requiring a custom provider response.</param>
    public RecordingEmbeddingClient(int dimensions = 3, Func<EmbeddingRequest, EmbeddingResponse>? responseFactory = null)
    {
        if (dimensions < 1) throw new ArgumentOutOfRangeException(nameof(dimensions));
        Dimensions = dimensions;
        this.responseFactory = responseFactory;
    }

    /// <summary>Gets the fixed vector dimension count returned by the default deterministic response.</summary>
    public int Dimensions { get; }

    /// <summary>Gets the ordered requests received by this client.</summary>
    public List<EmbeddingRequest> Requests { get; } = [];

    /// <summary>Gets the number of completed embedding invocations.</summary>
    public int InvocationCount => Requests.Count;

    /// <summary>Gets or sets the exception raised before any response is produced.</summary>
    public Exception? ExceptionToThrow { get; set; }

    /// <inheritdoc />
    public Task<EmbeddingResponse> EmbedAsync(EmbeddingRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add(request);
        if (ExceptionToThrow is not null) throw ExceptionToThrow;
        return Task.FromResult(responseFactory?.Invoke(request) ?? new EmbeddingResponse(request.Inputs.Select((input, index) => new EmbeddingResult(index, CreateVector(input), Dimensions)).ToList()));
    }

    private IReadOnlyList<float> CreateVector(string input)
    {
        var vector = new float[Dimensions];
        for (var index = 0; index < input.Length; index++) vector[index % Dimensions] += input[index] / 255f;
        return vector;
    }
}

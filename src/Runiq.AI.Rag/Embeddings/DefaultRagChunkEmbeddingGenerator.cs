using Microsoft.Extensions.Options;
using Runiq.AI.Core.AI.Embeddings;
using Runiq.AI.Core.Configuration;
using Runiq.AI.Core.Models;
using Runiq.AI.Rag.Abstractions.Embeddings;
using Runiq.AI.Rag.Configuration;
using Runiq.AI.Rag.Embeddings;
using Runiq.AI.Rag.Models.Documents;
using Runiq.AI.Rag.Models.Embeddings;

namespace Runiq.AI.Rag.Embeddings;

/// <summary>
/// Coordinates ordered chunk embedding generation using provider-neutral inputs and a text embedding provider.
/// </summary>
public sealed class DefaultRagChunkEmbeddingGenerator : IRagChunkEmbeddingGenerator
{
    private readonly IEmbeddingClient embeddingClient;
    private readonly IRagEmbeddingInputPreparer inputPreparer;
    private readonly RagOptions options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultRagChunkEmbeddingGenerator"/> class.
    /// </summary>
    /// <param name="embeddingClient">The Core embedding client used to generate each chunk embedding.</param>
    /// <param name="inputPreparer">The provider-neutral input preparer used before provider calls.</param>
    /// <param name="options">The RAG options used to resolve the embedding model.</param>
    public DefaultRagChunkEmbeddingGenerator(
        IEmbeddingClient embeddingClient,
        IRagEmbeddingInputPreparer inputPreparer,
        IOptions<RagOptions>? options = null)
    {
        this.embeddingClient = embeddingClient ?? throw new ArgumentNullException(nameof(embeddingClient));
        this.inputPreparer = inputPreparer ?? throw new ArgumentNullException(nameof(inputPreparer));
        this.options = options?.Value ?? new RagOptions();
    }

    /// <summary>
    /// Generates embeddings for chunks sequentially and returns results in the same order as the input chunks.
    /// </summary>
    /// <param name="chunks">The ordered chunks to generate embeddings for.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>The generated chunk embedding results in input order.</returns>
    public async Task<IReadOnlyList<RagChunkEmbeddingResult>> GenerateAsync(
        IReadOnlyList<RagChunk> chunks,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chunks);
        cancellationToken.ThrowIfCancellationRequested();

        if (chunks.Count == 0)
        {
            return Array.Empty<RagChunkEmbeddingResult>();
        }

        var inputs = new List<RagEmbeddingInput>(chunks.Count);

        for (var index = 0; index < chunks.Count; index++)
        {
            var chunk = chunks[index] ?? throw new InvalidOperationException(
                $"Chunk embedding generation failed at input index {index} because the chunk is null.");

            var input = await inputPreparer.PrepareAsync(chunk, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The embedding input preparer returned null.");
            inputs.Add(input);
        }
        var response = await embeddingClient.EmbedAsync(new EmbeddingRequest(ResolveModel(), inputs.Select(input => input.Content).ToList(), Dimensions: ResolveModel().EmbeddingDimensions), cancellationToken).ConfigureAwait(false);
        if (response.Results.Count != inputs.Count)
            throw new InvalidOperationException("The embedding client returned a result count that does not match the input count.");
        return response.Results.OrderBy(result => result.Index).Select((result, index) => new RagChunkEmbeddingResult
        {
            ChunkId = inputs[index].ChunkId,
            DocumentId = inputs[index].DocumentId,
            ChunkIndex = inputs[index].ChunkIndex,
            Embedding = new RagEmbedding(result.Vector),
        }).ToList();
    }

    private ModelReference ResolveModel()
    {
        if (string.IsNullOrWhiteSpace(options.EmbeddingModel)) return ModelReference.Parse("openai/rag-embedding");
        return ProviderModelReferenceResolver.Resolve(ModelReference.Parse(options.EmbeddingModel), options.EmbeddingProvider);
    }
}


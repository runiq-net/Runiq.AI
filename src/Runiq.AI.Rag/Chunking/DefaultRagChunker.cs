using Microsoft.Extensions.Options;
using Runiq.AI.Rag.Abstractions.Chunking;
using Runiq.AI.Rag.Configuration;
using Runiq.AI.Rag.Models.Documents;
using Runiq.AI.Rag.Models.Metadata;
using System.Globalization;

namespace Runiq.AI.Rag.Chunking;

/// <summary>
/// Provides the default character-based RAG document chunker.
/// </summary>
public sealed class DefaultRagChunker : IRagChunker
{
    private readonly IOptions<RagOptions> options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultRagChunker"/> class.
    /// </summary>
    /// <param name="options">The RAG options that provide chunking settings.</param>
    public DefaultRagChunker(IOptions<RagOptions> options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Splits the specified document content into ordered character-based chunks.
    /// </summary>
    /// <param name="document">The source document to split into chunks.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>The ordered chunks extracted from the document.</returns>
    public Task<IReadOnlyList<RagChunk>> ChunkAsync(
        RagDocument document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrEmpty(document.Content))
        {
            return Task.FromResult<IReadOnlyList<RagChunk>>(Array.Empty<RagChunk>());
        }

        var chunkingOptions = options.Value.Chunking;
        Validate(chunkingOptions);

        var chunks = new List<RagChunk>();
        var startIndex = 0;
        var chunkIndex = 0;
        var step = chunkingOptions.MaxChunkLength - chunkingOptions.ChunkOverlap;

        while (startIndex < document.Content.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var length = Math.Min(chunkingOptions.MaxChunkLength, document.Content.Length - startIndex);
            var content = document.Content.Substring(startIndex, length);
            var endIndex = startIndex + length;
            var tokenCount = CountApproximateTokens(content);

            chunks.Add(new RagChunk
            {
                Id = BuildChunkId(document.Id, chunkIndex),
                DocumentId = document.Id,
                Content = content,
                Index = chunkIndex,
                Metadata = new RagChunkMetadata
                {
                    StartIndex = startIndex,
                    EndIndex = endIndex,
                    TokenCount = tokenCount,
                    AdditionalMetadata = BuildChunkMetadata(document.Metadata, startIndex, endIndex, tokenCount),
                },
            });

            if (endIndex >= document.Content.Length)
            {
                break;
            }

            startIndex += step;
            chunkIndex++;
        }

        return Task.FromResult<IReadOnlyList<RagChunk>>(chunks);
    }

    private static string BuildChunkId(string documentId, int chunkIndex)
    {
        return $"{documentId}:chunk:{chunkIndex}";
    }

    private static int CountApproximateTokens(string content)
    {
        return content
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Length;
    }

    private static RagMetadata BuildChunkMetadata(
        RagDocumentMetadata documentMetadata,
        int startIndex,
        int endIndex,
        int tokenCount)
    {
        var values = new Dictionary<string, string>();

        AddIfPresent(values, "sourceId", documentMetadata.SourceId);
        AddIfPresent(values, "sourceName", documentMetadata.SourceName);
        AddIfPresent(values, "sourceUri", documentMetadata.SourceUri);
        AddIfPresent(values, "contentType", documentMetadata.ContentType);
        AddIfPresent(values, "createdAt", documentMetadata.CreatedAt?.ToString("O", CultureInfo.InvariantCulture));
        AddIfPresent(values, "updatedAt", documentMetadata.UpdatedAt?.ToString("O", CultureInfo.InvariantCulture));

        foreach (var (key, value) in documentMetadata.AdditionalMetadata.Values)
        {
            if (IsChunkTechnicalMetadataKey(key))
            {
                continue;
            }

            values[key] = value;
        }

        // Technical chunk values are canonicalized so source metadata cannot spoof chunk boundaries.
        values["startIndex"] = startIndex.ToString(CultureInfo.InvariantCulture);
        values["endIndex"] = endIndex.ToString(CultureInfo.InvariantCulture);
        values["tokenCount"] = tokenCount.ToString(CultureInfo.InvariantCulture);

        return new RagMetadata(values);
    }

    private static void AddIfPresent(Dictionary<string, string> values, string key, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            values[key] = value;
        }
    }

    private static bool IsChunkTechnicalMetadataKey(string key)
    {
        return string.Equals(key, "startIndex", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "endIndex", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "tokenCount", StringComparison.OrdinalIgnoreCase);
    }

    private static void Validate(RagChunkingOptions options)
    {
        if (options.MaxChunkLength <= 0)
        {
            throw new InvalidOperationException("RAG chunking MaxChunkLength must be greater than 0.");
        }

        if (options.ChunkOverlap < 0)
        {
            throw new InvalidOperationException("RAG chunking ChunkOverlap must be greater than or equal to 0.");
        }

        if (options.ChunkOverlap >= options.MaxChunkLength)
        {
            throw new InvalidOperationException("RAG chunking ChunkOverlap must be smaller than MaxChunkLength.");
        }
    }
}


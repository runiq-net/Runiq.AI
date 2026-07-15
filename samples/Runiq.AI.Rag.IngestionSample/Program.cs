using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Runiq.AI.Rag.Abstractions.Embeddings;
using Runiq.AI.Rag.Abstractions.Services;
using Runiq.AI.Rag.Configuration;
using Runiq.AI.Rag.DependencyInjection;
using Runiq.AI.Rag.Models.Documents;
using Runiq.AI.Rag.Models.Metadata;
using Runiq.AI.Rag.IngestionSample;

var services = new ServiceCollection();

services.AddRuniqRag();
services.AddRagEmbeddingProvider<DeterministicSampleEmbeddingProvider>();

services.Configure<RagOptions>(options =>
{
    options.Chunking.MaxChunkLength = 360;
    options.Chunking.ChunkOverlap = 40;
});

using var serviceProvider = services.BuildServiceProvider();
using var scope = serviceProvider.CreateScope();

var documentContent = await SampleDocumentReader.ReadAsync().ConfigureAwait(false);
var document = CreateDocument(documentContent);
var ingestionService = scope.ServiceProvider.GetRequiredService<IRagDocumentIngestionService>();
var ragOptions = scope.ServiceProvider.GetRequiredService<IOptions<RagOptions>>().Value;
var result = await ingestionService.IngestAsync(document).ConfigureAwait(false);

Console.WriteLine("Runiq RAG Ingestion Sample");
Console.WriteLine("==========================");
Console.WriteLine($"Input File: {documentContent.Path}");
Console.WriteLine($"Document Id: {document.Id}");
Console.WriteLine($"Source Name: {document.Metadata.SourceName}");
Console.WriteLine($"Chunking: MaxChunkLength={ragOptions.Chunking.MaxChunkLength}, ChunkOverlap={ragOptions.Chunking.ChunkOverlap}");
Console.WriteLine($"Chunks Generated: {result.Chunks.Count}");
Console.WriteLine();

foreach (var item in result.Items)
{
    var chunk = item.Chunk;
    var embedding = item.EmbeddingResult;

    Console.WriteLine($"Chunk Id: {chunk.Id}");
    Console.WriteLine($"  Document Id: {chunk.DocumentId}");
    Console.WriteLine($"  Chunk Index: {chunk.Index}");
    Console.WriteLine($"  Preview: {CreatePreview(chunk.Content)}");
    Console.WriteLine(
        $"  Metadata: StartIndex={FormatNullable(chunk.Metadata.StartIndex)}, EndIndex={FormatNullable(chunk.Metadata.EndIndex)}, TokenCount={FormatNullable(chunk.Metadata.TokenCount)}");
    Console.WriteLine($"  Embedding: ChunkId={embedding.ChunkId}, ChunkIndex={embedding.ChunkIndex}, Dimensions={embedding.Embedding.Dimensions}");
    Console.WriteLine($"  Association: chunk '{chunk.Id}' -> embedding for chunk '{embedding.ChunkId}'");
    Console.WriteLine();
}

static RagDocument CreateDocument(SampleDocumentContent documentContent)
{
    // The sample intentionally reads its content from the checked-in txt file so the console output is reproducible.
    return new RagDocument
    {
        Id = "sample-document",
        Content = documentContent.Content,
        Metadata = new RagDocumentMetadata
        {
            SourceId = "rag-ingestion-sample",
            SourceName = Path.GetFileName(documentContent.Path),
            SourceUri = documentContent.Path,
            ContentType = "text/plain",
            AdditionalMetadata = new RagMetadata(new Dictionary<string, string>
            {
                ["sample"] = "true",
                ["purpose"] = "document-chunk-embedding-flow",
            }),
        },
    };
}

static string CreatePreview(string content)
{
    const int maxLength = 120;

    var preview = content
        .ReplaceLineEndings(" ")
        .Trim();

    return preview.Length <= maxLength
        ? preview
        : string.Concat(preview.AsSpan(0, maxLength), "...");
}

static string FormatNullable(int? value)
{
    return value?.ToString() ?? "n/a";
}


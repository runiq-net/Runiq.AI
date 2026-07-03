using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Runiq.Rag.Abstractions.Services;
using Runiq.Rag.Abstractions.VectorStores;
using Runiq.Rag.Configuration;
using Runiq.Rag.DependencyInjection;
using Runiq.Rag.Models.Documents;
using Runiq.Rag.Models.Metadata;
using Runiq.Rag.Models.VectorStores;
using Runiq.Rag.UpsertPipelineSample;

// The explicit index name makes it obvious in the console output where the vector records are written.
const string IndexName = "sample-upsert-index";

var services = new ServiceCollection();

// The existing RAG DI entry point wires chunking, embedding generation, vector record mapping,
// dimension validation, and the upsert pipeline; the builder swaps in the in-memory vector store.
services.AddRuniqRag(builder => builder.UseInMemoryVectorStore());
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

var vectorStore = scope.ServiceProvider.GetRequiredService<IRagVectorStore>();
var ingestionService = scope.ServiceProvider.GetRequiredService<IRagDocumentIngestionService>();
var upsertPipeline = scope.ServiceProvider.GetRequiredService<IRagVectorStoreUpsertPipeline>();
var ragOptions = scope.ServiceProvider.GetRequiredService<IOptions<RagOptions>>().Value;

// The in-memory store requires the index to exist before records can be written, and creating it with
// the provider's dimension count lets the existing dimension validation protect every upserted record.
var indexResult = await vectorStore.CreateIndexAsync(new CreateVectorIndexRequest
{
    IndexName = IndexName,
    Dimensions = DeterministicSampleEmbeddingProvider.Dimensions,
}).ConfigureAwait(false);

// Step 1 + 2: chunk the document and generate one deterministic embedding per chunk.
var ingestionResult = await ingestionService.IngestAsync(document).ConfigureAwait(false);

// Step 3: hand the chunk/embedding pairs to the upsert pipeline, which maps them into vector
// records, validates the dimensions, and writes them to the in-memory store under the index name.
var upsertResult = await upsertPipeline.UpsertAsync(
    ingestionResult,
    IndexName,
    document.Metadata,
    expectedDimensions: DeterministicSampleEmbeddingProvider.Dimensions).ConfigureAwait(false);

Console.WriteLine("Runiq RAG Vector Store Upsert Pipeline Sample");
Console.WriteLine("=============================================");
Console.WriteLine($"Input File: {documentContent.Path}");
Console.WriteLine($"Document Id: {document.Id}");
Console.WriteLine($"Index Name: {IndexName} (created: {indexResult.Succeeded}, dimensions: {DeterministicSampleEmbeddingProvider.Dimensions})");
Console.WriteLine($"Chunking: MaxChunkLength={ragOptions.Chunking.MaxChunkLength}, ChunkOverlap={ragOptions.Chunking.ChunkOverlap}");
Console.WriteLine($"Chunks Generated: {ingestionResult.Chunks.Count}");
Console.WriteLine();

foreach (var item in ingestionResult.Items)
{
    var chunk = item.Chunk;
    var embedding = item.EmbeddingResult;

    Console.WriteLine($"Chunk Id: {chunk.Id}");
    Console.WriteLine($"  Document Id: {chunk.DocumentId}");
    Console.WriteLine($"  Chunk Index: {chunk.Index}");
    Console.WriteLine($"  Preview: {CreatePreview(chunk.Content)}");
    Console.WriteLine($"  Embedding Dimensions: {embedding.Embedding.Dimensions}");
    Console.WriteLine($"  Upsert Record: chunk '{chunk.Id}' -> index '{IndexName}'");
    Console.WriteLine();
}

Console.WriteLine("Upsert Result");
Console.WriteLine("-------------");
Console.WriteLine($"Succeeded: {upsertResult.Succeeded}");
Console.WriteLine($"Index Name: {upsertResult.IndexName}");
Console.WriteLine($"Attempted Count: {upsertResult.AttemptedCount}");
Console.WriteLine($"Processed Count: {upsertResult.ProcessedCount}");
Console.WriteLine($"Failed Count: {upsertResult.FailedCount}");
Console.WriteLine($"Error Code: {upsertResult.ErrorCode}");

if (!upsertResult.Succeeded)
{
    Console.WriteLine($"Reason: {upsertResult.Reason}");
}

Console.WriteLine($"Vector Ids: {string.Join(", ", upsertResult.VectorIds)}");

static RagDocument CreateDocument(SampleDocumentContent documentContent)
{
    // The sample intentionally reads its content from the checked-in txt file so the console output is reproducible.
    return new RagDocument
    {
        Id = "sample-upsert-document",
        Content = documentContent.Content,
        Metadata = new RagDocumentMetadata
        {
            SourceId = "rag-upsert-pipeline-sample",
            SourceName = Path.GetFileName(documentContent.Path),
            SourceUri = documentContent.Path,
            ContentType = "text/plain",
            AdditionalMetadata = new RagMetadata(new Dictionary<string, string>
            {
                ["sample"] = "true",
                ["purpose"] = "document-chunk-embedding-upsert-flow",
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

using Microsoft.Extensions.DependencyInjection;
using Runiq.AI.Rag.Abstractions.Services;
using Runiq.AI.Rag.Configuration;
using Runiq.AI.Rag.DependencyInjection;
using Runiq.AI.Rag.Models.Metadata;
using Runiq.AI.Rag.Models.Ingestion;
using Runiq.AI.Rag.IngestionSample;

var services = new ServiceCollection();

services.AddRuniqRag(builder => builder.UseInMemoryVectorStore());
services.AddRagEmbeddingClient<DeterministicSampleEmbeddingProvider>();

services.Configure<RagOptions>(options =>
{
    options.Chunking.MaxChunkLength = 360;
    options.Chunking.ChunkOverlap = 40;
});

using var serviceProvider = services.BuildServiceProvider();
using var scope = serviceProvider.CreateScope();

var documentContent = await SampleDocumentReader.ReadAsync().ConfigureAwait(false);
var document = CreateDocument(documentContent);
var rag = scope.ServiceProvider.GetRequiredService<IRagService>();
var result = await rag.IngestAsync(document, "sample-documents").ConfigureAwait(false);

Console.WriteLine("Runiq RAG Ingestion Sample");
Console.WriteLine("==========================");
Console.WriteLine($"Input File: {documentContent.Path}");
Console.WriteLine($"Document Id: {document.Id}");
Console.WriteLine($"Created: {result.CreatedDocuments}; Updated: {result.UpdatedDocuments}; Skipped: {result.SkippedDocuments}; Chunks: {result.CreatedChunks}");

static RagSourceDocument CreateDocument(SampleDocumentContent documentContent)
{
    // The sample intentionally reads its content from the checked-in txt file so the console output is reproducible.
    return new RagSourceDocument
    {
        Id = "sample-document",
        Content = documentContent.Content,
        Title = Path.GetFileName(documentContent.Path), Source = documentContent.Path, ContentType = "text/plain",
        Metadata = new RagMetadata(new Dictionary<string, string> { ["sample"] = "true", ["tenant"] = "demo" }),
    };
}


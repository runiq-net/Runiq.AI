using Microsoft.Extensions.Options;
using Runiq.AI.Agents;
using Runiq.AI.Agents.Configuration;
using Runiq.AI.Core;
using Runiq.AI.Rag.Configuration;
using Runiq.AI.Rag.CorporateDocumentAssistant.Models;
using Runiq.AI.Rag.CorporateDocumentAssistant.Services;
using Runiq.AI.Rag.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRuniqServer(options =>
{
    options.AddAgent(new Agent(
            id: "corporate-policy-assistant",
            name: "Corporate Policy Assistant",
            instructions: "Answer employee policy questions from the supplied reference material.",
            model: "ollama/llama3")
        .UseRag(rag =>
        {
            rag.IndexName = "corporate-document-assistant";
            rag.Mode = RagExecutionMode.Required;
        }));
});
builder.Services.AddRuniqRag(ragBuilder => ragBuilder.UseInMemoryVectorStore());
builder.Services.AddRagEmbeddingClient<DeterministicCorporateEmbeddingProvider>();
builder.Services.Configure<RagOptions>(options =>
{
    options.DefaultIndexName = "corporate-document-assistant";
    options.Chunking.MaxChunkLength = 360;
    options.Chunking.ChunkOverlap = 40;
});
builder.Services.Configure<CorporateDocumentAssistantOptions>(
    builder.Configuration.GetSection("CorporateDocumentAssistant"));
builder.Services.AddSingleton<SeedDocumentReader>();
builder.Services.AddScoped<CorporateDocumentIngestionService>();
builder.Services.AddScoped<CorporateDocumentQueryService>();

var app = builder.Build();

app.UseRuniqDashboard(options =>
{
    options.Path = "/dashboard";
    options.Title = "Runiq Corporate Document Assistant";
    options.Authentication(auth =>
    {
        // Demo/sample only. Do not use AllowAnonymous in production.
        auth.AllowAnonymous();
    });
});

app.MapGet("/", () => Results.Content(
    """
    <!doctype html>
    <html lang="en">
    <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>Corporate Document Assistant</title>
        <style>
            body { color: #1f2937; font-family: Arial, sans-serif; line-height: 1.5; margin: 2rem auto; max-width: 52rem; padding: 0 1rem; }
            h1 { color: #0f172a; }
            code { background: #f3f4f6; border-radius: .25rem; padding: .125rem .25rem; }
            a { color: #0f766e; }
        </style>
    </head>
    <body>
        <h1>Corporate Document Assistant</h1>
        <p>This ASP.NET Core sample is the host shell for a provider-independent RAG demo.</p>
        <p>Open the embedded Runiq Dashboard at <a href="/dashboard">/dashboard</a>.</p>
        <p>Seed documents are available at <a href="/documents">/documents</a>. Ingest them with <code>POST /ingestion/seed</code>.</p>
        <p>Ask questions with <code>POST /query</code> after ingesting documents.</p>
    </body>
    </html>
    """,
    "text/html"));

app.MapGet("/documents", async (SeedDocumentReader reader, CancellationToken cancellationToken) =>
{
    var documents = await reader.ReadSummariesAsync(cancellationToken).ConfigureAwait(false);

    return Results.Ok(documents);
});

app.MapGet("/documents/{id}", async (string id, SeedDocumentReader reader, CancellationToken cancellationToken) =>
{
    var document = await reader.ReadDocumentAsync(id, cancellationToken).ConfigureAwait(false);

    if (document is null)
    {
        return Results.NotFound();
    }

    return Results.Text(document.Content, "text/plain");
});

app.MapPost("/ingestion/documents", async (
    CorporateDocumentIngestionRequest request,
    CorporateDocumentIngestionService ingestionService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var response = await ingestionService.IngestAsync(request, cancellationToken).ConfigureAwait(false);

        return Results.Ok(response);
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
});

app.MapPost("/ingestion/seed", async (
    SeedDocumentReader reader,
    CorporateDocumentIngestionService ingestionService,
    CancellationToken cancellationToken) =>
{
    var seedDocuments = await reader.ReadDocumentsAsync(cancellationToken).ConfigureAwait(false);
    var responses = new List<CorporateDocumentIngestionResponse>();

    foreach (var document in seedDocuments)
    {
        responses.Add(await ingestionService.IngestAsync(document, cancellationToken).ConfigureAwait(false));
    }

    return Results.Ok(responses);
});

app.MapPost("/query", async (
    CorporateDocumentQueryRequest request,
    CorporateDocumentQueryService queryService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var response = await queryService.AskAsync(request, cancellationToken).ConfigureAwait(false);

        return Results.Ok(response);
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (InvalidOperationException exception)
    {
        return Results.Problem(exception.Message);
    }
});

app.MapGet("/ingestion", (IOptions<CorporateDocumentAssistantOptions> options) =>
{
    return Results.Ok(new
    {
        options.Value.IndexName,
        EmbeddingDimensions = DeterministicCorporateEmbeddingProvider.Dimensions,
        Endpoints = new[]
        {
            "POST /ingestion/seed",
            "POST /ingestion/documents",
            "POST /query",
        },
    });
});

app.Run();


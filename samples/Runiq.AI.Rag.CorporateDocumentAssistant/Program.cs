using Runiq.AI.Core;
using Runiq.AI.Rag.CorporateDocumentAssistant;
using Runiq.AI.Rag.Abstractions.Retrieval;
using Runiq.AI.Rag.Models.Queries;
using Runiq.AI.Rag.Models.Retrieval;

var builder = WebApplication.CreateBuilder(args);

var documentsPath = Path.Combine(AppContext.BaseDirectory, "SampleDocuments");
CorporateDocumentAssistantSetup.Configure(builder.Services, builder.Configuration, documentsPath);

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

app.MapGet("/", () => Results.Redirect("/dashboard"));

app.MapGet("/retrieval-demo/{mode}", async (
    string mode,
    string? query,
    IRagRetriever retriever,
    CancellationToken cancellationToken) =>
{
    if (!Enum.TryParse<RagRetrievalMode>(mode, ignoreCase: true, out var retrievalMode) ||
        !Enum.IsDefined(retrievalMode))
    {
        return Results.BadRequest("Mode must be Semantic, Lexical, or Hybrid.");
    }

    var effectiveQuery = string.IsNullOrWhiteSpace(query) ? "IRagRetriever" : query;
    var result = await retriever.RetrieveWithMetadataAsync(new RagQuery
    {
        IndexName = CorporateDocumentAssistantSetup.IndexName,
        Text = effectiveQuery,
        TopK = 5,
        Mode = retrievalMode,
    }, cancellationToken);

    return Results.Ok(new
    {
        retrievalMode,
        query = effectiveQuery,
        result.Statistics,
        candidates = result.Candidates.Select(candidate => new
        {
            candidate.Chunk.DocumentId,
            candidate.Chunk.Id,
            candidate.RawScore,
            candidate.Relevance,
            candidate.Metric,
            candidate.HigherIsBetter,
            candidate.Provenance,
        }),
    });
});

app.Run();

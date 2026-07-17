using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Runiq.AI.Rag.Configuration;
using Runiq.AI.Rag.Runtime;

namespace Runiq.AI.Rag.Hosting;

internal static class RagManagementEndpointExtensions
{
    public static IEndpointRouteBuilder MapRuniqRagManagementApi(this IEndpointRouteBuilder endpoints, string pathPrefix)
    {
        var group = endpoints.MapGroup(pathPrefix);
        group.MapGet("/rag/indexes", ListIndexes);
        group.MapGet("/rag/indexes/{indexName}", GetIndex);
        group.MapGet("/rag/indexes/{indexName}/status", GetStatus);
        group.MapPost("/rag/indexes/{indexName}/ingestion/start", Start);
        group.MapPost("/rag/indexes/{indexName}/ingestion/cancel", Cancel);
        return endpoints;
    }

    private static IResult ListIndexes(IRagIndexRegistry registry, IRagIngestionManager manager) =>
        Results.Ok(registry.GetMetadata().Select(metadata => RagManagementMapper.MapListItem(metadata, manager.GetStatus(metadata.Name))).ToArray());

    private static IResult GetIndex(string indexName, IRagIndexRegistry registry, IRagIngestionManager manager)
    {
        var metadata = Find(registry, indexName);
        return metadata is null ? NotFound(indexName) : Results.Ok(RagManagementMapper.MapDetail(metadata, manager.GetStatus(metadata.Name)));
    }

    private static IResult GetStatus(string indexName, IRagIndexRegistry registry, IRagIngestionManager manager)
    {
        var metadata = Find(registry, indexName);
        return metadata is null ? NotFound(indexName) : Results.Ok(RagManagementMapper.Map(manager.GetStatus(metadata.Name)));
    }

    private static IResult Start(string indexName, IRagIndexRegistry registry, IRagIngestionManager manager, ILogger<RagIngestionManager> logger, CancellationToken cancellationToken)
    {
        var metadata = Find(registry, indexName);
        if (metadata is null) return NotFound(indexName);
        logger.LogInformation("Manual RAG ingestion start requested for index {IndexName}.", metadata.Name);
        try
        {
            _ = manager.StartAsync(metadata.Name, cancellationToken);
            var status = manager.GetStatus(metadata.Name);
            var operation = status.ActiveOperation ?? status.LastOperation!;
            logger.LogInformation("Manual RAG ingestion start accepted for index {IndexName} as operation {OperationId}.", metadata.Name, operation.OperationId);
            return Results.Accepted(value: RagManagementMapper.Map(operation));
        }
        catch (InvalidOperationException)
        {
            var active = manager.GetStatus(metadata.Name).ActiveOperation;
            logger.LogInformation("Manual RAG ingestion start conflicted for index {IndexName}; active operation {OperationId}.", metadata.Name, active?.OperationId);
            return Results.Conflict(new RagManagementErrorDto("ActiveIngestionOperation", "The index already has an active ingestion operation.", RagManagementMapper.Map(active)));
        }
    }

    private static async Task<IResult> Cancel(string indexName, IRagIndexRegistry registry, IRagIngestionManager manager, ILogger<RagIngestionManager> logger, CancellationToken cancellationToken)
    {
        var metadata = Find(registry, indexName);
        if (metadata is null) return NotFound(indexName);
        logger.LogInformation("RAG ingestion cancellation requested for index {IndexName}.", metadata.Name);
        if (manager.GetStatus(metadata.Name).ActiveOperation is null)
        {
            logger.LogInformation("RAG ingestion cancellation conflicted for index {IndexName} because no operation is active.", metadata.Name);
            return Results.Conflict(new RagManagementErrorDto("NoActiveIngestionOperation", "The index has no active ingestion operation."));
        }
        await manager.CancelAsync(metadata.Name, cancellationToken).ConfigureAwait(false);
        var operation = manager.GetStatus(metadata.Name).LastOperation;
        logger.LogInformation("RAG ingestion cancellation accepted for index {IndexName}; operation {OperationId} is {OperationState}.", metadata.Name, operation?.OperationId, operation?.State);
        return Results.Ok(RagManagementMapper.Map(operation));
    }

    private static RagIndexMetadata? Find(IRagIndexRegistry registry, string indexName) => registry.GetMetadata().SingleOrDefault(metadata => string.Equals(metadata.Name, indexName, StringComparison.Ordinal));
    private static IResult NotFound(string indexName) => Results.NotFound(new RagManagementErrorDto("RagIndexNotFound", $"RAG index '{indexName}' is not registered."));
}

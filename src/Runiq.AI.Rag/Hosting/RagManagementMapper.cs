using Runiq.AI.Rag.Configuration;
using Runiq.AI.Rag.Runtime;
using System.Security.Cryptography;
using System.Text;

namespace Runiq.AI.Rag.Hosting;

internal static class RagManagementMapper
{
    public static RagIndexListItemDto MapListItem(RagIndexMetadata metadata, RagIndexRuntimeStatus status) =>
        new(metadata.Name, metadata.SourceCount, metadata.Sources.Select(Map).ToArray(), Map(metadata), status.Readiness.ToString(), Map(status.ActiveOperation), Map(status.LastOperation));

    public static RagIndexDetailDto MapDetail(RagIndexMetadata metadata, RagIndexRuntimeStatus status) =>
        new(new(metadata.Name, metadata.SourceCount), metadata.Sources.Select(Map).ToArray(), Map(metadata), Map(status));

    public static RagIndexRuntimeStatusDto Map(RagIndexRuntimeStatus status)
    {
        var current = status.ActiveOperation ?? status.LastOperation;
        return new(status.IndexName, status.Readiness.ToString(), Map(status.ActiveOperation), Map(status.LastOperation), current is null ? null : Map(current.Progress), Map(current?.Progress.LastFailure), status.LastUpdatedAt);
    }

    public static RagOperationDto? Map(RagIngestionOperation? operation) => operation is null ? null :
        new(operation.OperationId, operation.IndexName, operation.Reason.ToString(), operation.State.ToString(), operation.StartedAt, operation.CompletedAt, operation.Duration.TotalMilliseconds, Map(operation.Progress));

    private static RagSourceDto Map(RagDocumentSourceMetadata source)
    {
        var identity = SafeIdentity(source.Identity)!;
        return new(identity, source.SourceType, $"Source {identity[..12]}");
    }
    private static RagIndexConfigurationDto Map(RagIndexMetadata metadata) => new(metadata.IngestionStrategyKind.ToString(), metadata.ScheduleExpression, metadata.VectorStoreType, metadata.VectorStoreDisplayName, metadata.EmbeddingDisplayName, metadata.ChunkSize, metadata.ChunkOverlap);
    private static RagProgressDto Map(RagIngestionProgress progress) => new(progress.DiscoveredDocuments, progress.ProcessedDocuments, progress.AddedDocuments, progress.UpdatedDocuments, progress.SkippedDocuments, progress.DeletedDocuments, progress.FailedDocuments, progress.ProducedChunks, progress.ProducedEmbeddings, SafeIdentity(progress.CurrentSource), SafeIdentity(progress.CurrentDocument));
    private static RagFailureDto? Map(RagIngestionRuntimeFailure? failure) => failure is null ? null :
        new(failure.Code, "The ingestion operation failed. Review server logs using the operation identity.",
            SafeIdentity(failure.SourceIdentity), SafeIdentity(failure.DocumentIdentity), failure.Timestamp);
    private static string? SafeIdentity(string? identity) => identity is null ? null : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
}

namespace Runiq.AI.Rag.Hosting;

internal sealed record RagIndexListItemDto(string Name, int SourceCount, IReadOnlyList<RagSourceDto> Sources, RagIndexConfigurationDto Configuration, string Readiness, RagOperationDto? ActiveOperation, RagOperationDto? LastOperation);
internal sealed record RagIndexDetailDto(RagIndexOverviewDto Overview, IReadOnlyList<RagSourceDto> Sources, RagIndexConfigurationDto Configuration, RagIndexRuntimeStatusDto Runtime);
internal sealed record RagIndexOverviewDto(string Name, int SourceCount);
internal sealed record RagSourceDto(string Identity, string Type, string DisplayValue);
internal sealed record RagIndexConfigurationDto(string IngestionStrategy, string? ScheduleExpression, string VectorStoreType, string VectorStoreReference, string EmbeddingReference, int ChunkSize, int ChunkOverlap);
internal sealed record RagIndexRuntimeStatusDto(string IndexName, string Readiness, RagOperationDto? ActiveOperation, RagOperationDto? LastOperation, RagProgressDto? Progress, RagFailureDto? LastFailure, DateTimeOffset LastUpdatedAt);
internal sealed record RagOperationDto(Guid OperationId, string IndexName, string Reason, string State, DateTimeOffset StartedAt, DateTimeOffset? CompletedAt, double DurationMilliseconds, RagProgressDto Progress);
internal sealed record RagProgressDto(int DiscoveredDocuments, int ProcessedDocuments, int AddedDocuments, int UpdatedDocuments, int SkippedDocuments, int DeletedDocuments, int FailedDocuments, int ProducedChunks, int ProducedEmbeddings, string? CurrentSourceIdentity, string? CurrentDocumentIdentity);
internal sealed record RagFailureDto(string Code, string Message, string? SourceIdentity, string? DocumentIdentity, DateTimeOffset Timestamp);
internal sealed record RagManagementErrorDto(string Code, string Message, RagOperationDto? ActiveOperation = null);

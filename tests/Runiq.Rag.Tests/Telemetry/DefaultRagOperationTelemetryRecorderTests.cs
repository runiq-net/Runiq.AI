using Runiq.Rag.Models.Retrieval;
using Runiq.Rag.Models.VectorStores;
using Runiq.Rag.Telemetry;

namespace Runiq.Rag.Tests.Telemetry;

public sealed class DefaultRagOperationTelemetryRecorderTests
{
    private static readonly DateTimeOffset RecordingTime = new(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void LastSnapshots_ShouldBeNull_BeforeAnyOperationIsRecorded()
    {
        // Verifies the no-operation-yet state: both snapshots are null instead of fabricated values.
        var recorder = CreateRecorder();

        Assert.Null(recorder.LastUpsert);
        Assert.Null(recorder.LastRetrieval);
    }

    [Fact]
    public void RecordUpsert_ShouldCaptureSuccessOutcome_WithProcessedChunkCount()
    {
        // Verifies that a successful upsert result is mapped into a snapshot carrying the processed
        // record count as the chunk count and the injected clock's timestamp.
        var recorder = CreateRecorder();

        recorder.RecordUpsert(new UpsertVectorResult
        {
            Succeeded = true,
            ErrorCode = VectorStoreUpsertErrorCode.None,
            ProcessedCount = 3,
            AttemptedCount = 3,
        });

        var snapshot = recorder.LastUpsert;
        Assert.NotNull(snapshot);
        Assert.True(snapshot.Succeeded);
        Assert.Equal(VectorStoreUpsertErrorCode.None, snapshot.ErrorCode);
        Assert.Equal(string.Empty, snapshot.Reason);
        Assert.Equal(3, snapshot.ChunkCount);
        Assert.Equal(RecordingTime, snapshot.Timestamp);
    }

    [Fact]
    public void RecordUpsert_ShouldCaptureFailureOutcome_WithAttemptedChunkCount()
    {
        // Verifies that a failed upsert result is mapped into a snapshot carrying the error code,
        // the developer-readable reason, and the attempted record count as the chunk count.
        var recorder = CreateRecorder();

        recorder.RecordUpsert(new UpsertVectorResult
        {
            Succeeded = false,
            ErrorCode = VectorStoreUpsertErrorCode.StoreFailed,
            Reason = "Vector store upsert failed.",
            ProcessedCount = 0,
            AttemptedCount = 5,
            FailedCount = 5,
        });

        var snapshot = recorder.LastUpsert;
        Assert.NotNull(snapshot);
        Assert.False(snapshot.Succeeded);
        Assert.Equal(VectorStoreUpsertErrorCode.StoreFailed, snapshot.ErrorCode);
        Assert.Equal("Vector store upsert failed.", snapshot.Reason);
        Assert.Equal(5, snapshot.ChunkCount);
        Assert.Equal(RecordingTime, snapshot.Timestamp);
    }

    [Fact]
    public void RecordRetrieval_ShouldCaptureSuccessOutcome_WithResultCountAndDuration()
    {
        // Verifies that a successful retrieval result is mapped into a snapshot carrying the match
        // count, the caller-measured duration, and the injected clock's timestamp.
        var recorder = CreateRecorder();
        var result = RetrievalResult.Success(
        [
            new RetrievalResultItem { RecordId = "chunk-1" },
            new RetrievalResultItem { RecordId = "chunk-2" },
        ]);

        recorder.RecordRetrieval(result, TimeSpan.FromMilliseconds(1500));

        var snapshot = recorder.LastRetrieval;
        Assert.NotNull(snapshot);
        Assert.True(snapshot.Succeeded);
        Assert.Equal(RetrievalErrorCode.None, snapshot.ErrorCode);
        Assert.Equal(string.Empty, snapshot.Reason);
        Assert.Equal(2, snapshot.ResultCount);
        Assert.Equal(TimeSpan.FromMilliseconds(1500), snapshot.Duration);
        Assert.Equal(RecordingTime, snapshot.Timestamp);
    }

    [Fact]
    public void RecordRetrieval_ShouldCaptureFailureOutcome_WithErrorCodeAndReason()
    {
        // Verifies that a failed retrieval result is mapped into a snapshot carrying the deterministic
        // error code, the developer-readable reason, and a zero result count.
        var recorder = CreateRecorder();
        var result = RetrievalResult.Failure(
            RetrievalErrorCode.VectorStoreQueryFailed,
            "Vector store query failed.");

        recorder.RecordRetrieval(result, TimeSpan.FromMilliseconds(20));

        var snapshot = recorder.LastRetrieval;
        Assert.NotNull(snapshot);
        Assert.False(snapshot.Succeeded);
        Assert.Equal(RetrievalErrorCode.VectorStoreQueryFailed, snapshot.ErrorCode);
        Assert.Equal("Vector store query failed.", snapshot.Reason);
        Assert.Equal(0, snapshot.ResultCount);
        Assert.Equal(TimeSpan.FromMilliseconds(20), snapshot.Duration);
    }

    [Fact]
    public void Record_ShouldIgnoreNullResults_WithoutChangingExistingSnapshots()
    {
        // Verifies the recorder's defensive contract: null results are ignored instead of throwing
        // or overwriting previously recorded snapshots.
        var recorder = CreateRecorder();
        recorder.RecordUpsert(new UpsertVectorResult { Succeeded = true, ProcessedCount = 1 });
        recorder.RecordRetrieval(RetrievalResult.Success(), TimeSpan.Zero);
        var upsertBefore = recorder.LastUpsert;
        var retrievalBefore = recorder.LastRetrieval;

        recorder.RecordUpsert(null!);
        recorder.RecordRetrieval(null!, TimeSpan.FromSeconds(1));

        Assert.Same(upsertBefore, recorder.LastUpsert);
        Assert.Same(retrievalBefore, recorder.LastRetrieval);
    }

    [Fact]
    public void Record_ShouldReplacePreviousSnapshot_WithLatestOperation()
    {
        // Verifies last-operation semantics: recording a new outcome replaces the previous snapshot.
        var recorder = CreateRecorder();
        recorder.RecordUpsert(new UpsertVectorResult { Succeeded = true, ProcessedCount = 2 });

        recorder.RecordUpsert(new UpsertVectorResult
        {
            Succeeded = false,
            ErrorCode = VectorStoreUpsertErrorCode.ValidationFailed,
            Reason = "Vector dimension does not match the index dimensions.",
            AttemptedCount = 4,
        });

        var snapshot = recorder.LastUpsert;
        Assert.NotNull(snapshot);
        Assert.False(snapshot.Succeeded);
        Assert.Equal(VectorStoreUpsertErrorCode.ValidationFailed, snapshot.ErrorCode);
        Assert.Equal(4, snapshot.ChunkCount);
    }

    private static DefaultRagOperationTelemetryRecorder CreateRecorder()
    {
        return new DefaultRagOperationTelemetryRecorder(new FixedTimeProvider(RecordingTime));
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            this.utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}

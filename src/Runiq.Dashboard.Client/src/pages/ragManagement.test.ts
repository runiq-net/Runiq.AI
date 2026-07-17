import assert from 'node:assert/strict';
import test from 'node:test';
import { parseRuntimeStatus, type RagIndexListItem, type RagRuntimeStatus } from '../api/ragApi.ts';
import { formatDuration, mergeRuntime, operationReasonLabels, operationStateLabels, pollingDelay, progressValue, readinessLabels, shouldApplyStatus, summarizeIndexes } from './ragManagement.ts';

// Verifies summary cards are derived only from truthful index-list readiness and active-operation fields.
test('summary derives registered index and active ingestion counts', () => {
  const indexes = [index('one', 'Ready', true), index('two', 'Degraded'), index('three', 'Failed'), index('four', 'Initializing')];
  assert.deepEqual(summarizeIndexes(indexes), { total: 4, ready: 1, initializing: 1, degraded: 1, failed: 1, active: 1 });
});

// Verifies determinate progress is bounded and unknown discovery totals remain indeterminate.
test('progress uses processed over discovered only with a known denominator', () => {
  assert.equal(progressValue(5, 10), 50);
  assert.equal(progressValue(12, 10), 100);
  assert.equal(progressValue(0, 0), null);
});

// Verifies runtime polling is frequent only during active work and pauses while the page is hidden.
test('polling cadence reflects operation activity and document visibility', () => {
  assert.equal(pollingDelay(status(true), true), 1500);
  assert.equal(pollingDelay(status(false), true), 15000);
  assert.equal(pollingDelay(status(true), false), null);
});

// Verifies stale polling responses cannot overwrite the most recently requested runtime snapshot.
test('stale response sequence is rejected', () => {
  assert.equal(shouldApplyStatus(4, 5), false);
  assert.equal(shouldApplyStatus(5, 5), true);
});

// Verifies a status refresh updates runtime fields without replacing immutable configuration metadata.
test('runtime status merges into the matching registered index only', () => {
  const indexes = [index('one', 'NotInitialized'), index('two', 'Ready')];
  const next = { ...status(true), indexName: 'one', readiness: 'Initializing' as const };
  const merged = mergeRuntime(indexes, next);
  assert.equal(merged[0]?.readiness, 'Initializing');
  assert.equal(merged[0]?.configuration.vectorStoreReference, 'safe-store');
  assert.equal(merged[1], indexes[1]);
});

// Verifies centralized labels preserve readiness and operation-state separation with user-facing wording.
test('status and reason labels are centralized', () => {
  assert.equal(readinessLabels.NotInitialized, 'Not initialized');
  assert.equal(operationStateLabels.PartiallyCompleted, 'Partially completed');
  assert.equal(operationReasonLabels.BackgroundStartup, 'Background startup');
  assert.equal(formatDuration(2500), '2.5 s');
});

// Verifies malformed status payloads and undefined enums are rejected before reaching React rendering.
test('runtime parser rejects malformed and undefined state payloads', () => {
  assert.throws(() => parseRuntimeStatus({}));
  assert.throws(() => parseRuntimeStatus({ ...statusPayload(), readiness: 'Unknown' }));
  assert.throws(() => parseRuntimeStatus({ ...statusPayload(), lastUpdatedAt: 'not-a-date' }));
});

// Verifies a complete safe runtime payload is parsed without diagnostic or content fields.
test('runtime parser accepts safe progress and failure summaries', () => {
  const parsed = parseRuntimeStatus(statusPayload());
  assert.equal(parsed.activeOperation?.state, 'Running');
  assert.equal(parsed.progress?.currentSourceIdentity, 'SAFE-SOURCE');
  assert.equal(parsed.lastFailure?.message, 'A document could not be ingested.');
  assert.equal('exception' in parsed, false);
});

function index(name: string, readiness: RagIndexListItem['readiness'], active = false): RagIndexListItem {
  const runtime = status(active);
  return { name, sourceCount: 1, sources: [{ identity: 'SAFE', type: 'Directory', displayValue: 'documents' }], configuration: { ingestionStrategy: 'Manual', scheduleExpression: null, vectorStoreType: 'PostgreSql', vectorStoreReference: 'safe-store', embeddingReference: 'safe-model', chunkSize: 512, chunkOverlap: 64 }, readiness, activeOperation: runtime.activeOperation, lastOperation: runtime.lastOperation };
}

function status(active: boolean): RagRuntimeStatus {
  const operation = active ? operationPayload() : null;
  return { indexName: 'one', readiness: active ? 'Initializing' : 'Ready', activeOperation: operation, lastOperation: null, progress: operation?.progress ?? null, lastFailure: null, lastUpdatedAt: '2026-07-17T10:00:00Z' };
}

function operationPayload() {
  return { operationId: 'operation-1', indexName: 'one', reason: 'Manual' as const, state: 'Running' as const, startedAt: '2026-07-17T10:00:00Z', completedAt: null, durationMilliseconds: 1000, progress: { discoveredDocuments: 10, processedDocuments: 5, addedDocuments: 3, updatedDocuments: 1, skippedDocuments: 1, deletedDocuments: 0, failedDocuments: 0, producedChunks: 8, producedEmbeddings: 8, currentSourceIdentity: 'SAFE-SOURCE', currentDocumentIdentity: 'SAFE-DOCUMENT' } };
}

function statusPayload() {
  return { indexName: 'one', readiness: 'Initializing', activeOperation: operationPayload(), lastOperation: null, progress: operationPayload().progress, lastFailure: { code: 'DocumentFailed', message: 'A document could not be ingested.', sourceIdentity: 'SAFE-SOURCE', documentIdentity: 'SAFE-DOCUMENT', timestamp: '2026-07-17T10:00:01Z' }, lastUpdatedAt: '2026-07-17T10:00:01Z' };
}

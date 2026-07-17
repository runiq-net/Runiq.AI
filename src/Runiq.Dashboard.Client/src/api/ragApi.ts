import { getDashboardBasePath } from '../dashboardConfig.ts';

export type RagReadiness = 'NotInitialized' | 'Initializing' | 'Ready' | 'Degraded' | 'Failed';
export type RagOperationState = 'Pending' | 'Running' | 'Cancelling' | 'Completed' | 'PartiallyCompleted' | 'Failed' | 'Cancelled';
export type RagOperationReason = 'Manual' | 'Startup' | 'BackgroundStartup' | 'Scheduled';
export type RagIngestionStrategy = 'Manual' | 'OnStartup' | 'BackgroundOnStartup' | 'Scheduled';

export type RagSource = { identity: string; type: string; displayValue: string };
export type RagIndexConfiguration = { ingestionStrategy: RagIngestionStrategy; scheduleExpression: string | null; vectorStoreType: string; vectorStoreReference: string; embeddingReference: string; chunkSize: number; chunkOverlap: number };
export type RagProgress = { discoveredDocuments: number; processedDocuments: number; addedDocuments: number; updatedDocuments: number; skippedDocuments: number; deletedDocuments: number; failedDocuments: number; producedChunks: number; producedEmbeddings: number; currentSourceIdentity: string | null; currentDocumentIdentity: string | null };
export type RagFailure = { code: string; message: string; sourceIdentity: string | null; documentIdentity: string | null; timestamp: string };
export type RagOperation = { operationId: string; indexName: string; reason: RagOperationReason; state: RagOperationState; startedAt: string; completedAt: string | null; durationMilliseconds: number; progress: RagProgress };
export type RagIndexListItem = { name: string; sourceCount: number; sources: RagSource[]; configuration: RagIndexConfiguration; readiness: RagReadiness; activeOperation: RagOperation | null; lastOperation: RagOperation | null };
export type RagRuntimeStatus = { indexName: string; readiness: RagReadiness; activeOperation: RagOperation | null; lastOperation: RagOperation | null; progress: RagProgress | null; lastFailure: RagFailure | null; lastUpdatedAt: string };
export type RagIndexDetail = { overview: { name: string; sourceCount: number }; sources: RagSource[]; configuration: RagIndexConfiguration; runtime: RagRuntimeStatus };
export type RagManagementError = { code: string; message: string; activeOperation: RagOperation | null };

export class RagApiError extends Error {
  readonly status: number;
  readonly conflict: RagManagementError | null;

  constructor(status: number, message: string, conflict: RagManagementError | null = null) {
    super(message);
    this.name = 'RagApiError';
    this.status = status;
    this.conflict = conflict;
  }
}

export function listRagIndexes(signal?: AbortSignal): Promise<RagIndexListItem[]> {
  return request('/api/rag/indexes', parseIndexList, { signal });
}

export function getRagIndex(indexName: string, signal?: AbortSignal): Promise<RagIndexDetail> {
  return request(`/api/rag/indexes/${encodeURIComponent(indexName)}`, parseIndexDetail, { signal });
}

export function getRagStatus(indexName: string, signal?: AbortSignal): Promise<RagRuntimeStatus> {
  return request(`/api/rag/indexes/${encodeURIComponent(indexName)}/status`, parseRuntimeStatus, { signal });
}

export function startRagIngestion(indexName: string, signal?: AbortSignal): Promise<RagOperation> {
  return request(`/api/rag/indexes/${encodeURIComponent(indexName)}/ingestion/start`, parseOperation, { method: 'POST', signal });
}

export function cancelRagIngestion(indexName: string, signal?: AbortSignal): Promise<RagOperation> {
  return request(`/api/rag/indexes/${encodeURIComponent(indexName)}/ingestion/cancel`, parseOperation, { method: 'POST', signal });
}

async function request<T>(path: string, parse: (value: unknown) => T, init?: RequestInit): Promise<T> {
  const basePath = getDashboardBasePath().replace(/\/+$/g, '');
  const response = await fetch(`${basePath}${path}`, init);
  const payload: unknown = await response.json().catch(() => null);
  if (!response.ok) {
    const error = parseManagementError(payload);
    const message = response.status === 401 || response.status === 403
      ? 'You are not authorized to manage RAG indexes.'
      : response.status === 404 ? 'The requested RAG index is no longer registered.'
        : response.status === 409 ? error?.message ?? 'The ingestion operation changed before the command completed.'
          : 'RAG management is temporarily unavailable.';
    throw new RagApiError(response.status, message, error);
  }
  try { return parse(payload); } catch { throw new RagApiError(response.status, 'RAG management returned an invalid response.'); }
}

const readinessValues: RagReadiness[] = ['NotInitialized', 'Initializing', 'Ready', 'Degraded', 'Failed'];
const operationStateValues: RagOperationState[] = ['Pending', 'Running', 'Cancelling', 'Completed', 'PartiallyCompleted', 'Failed', 'Cancelled'];
const operationReasonValues: RagOperationReason[] = ['Manual', 'Startup', 'BackgroundStartup', 'Scheduled'];
const strategyValues: RagIngestionStrategy[] = ['Manual', 'OnStartup', 'BackgroundOnStartup', 'Scheduled'];

function parseIndexList(value: unknown): RagIndexListItem[] { return array(value).map(parseIndexListItem); }
function parseIndexListItem(value: unknown): RagIndexListItem { const o = record(value); return { name: text(o.name), sourceCount: count(o.sourceCount), sources: array(o.sources).map(parseSource), configuration: parseConfiguration(o.configuration), readiness: enumValue(o.readiness, readinessValues), activeOperation: nullable(o.activeOperation, parseOperation), lastOperation: nullable(o.lastOperation, parseOperation) }; }
function parseIndexDetail(value: unknown): RagIndexDetail { const o = record(value); const overview = record(o.overview); return { overview: { name: text(overview.name), sourceCount: count(overview.sourceCount) }, sources: array(o.sources).map(parseSource), configuration: parseConfiguration(o.configuration), runtime: parseRuntimeStatus(o.runtime) }; }
export function parseRuntimeStatus(value: unknown): RagRuntimeStatus { const o = record(value); return { indexName: text(o.indexName), readiness: enumValue(o.readiness, readinessValues), activeOperation: nullable(o.activeOperation, parseOperation), lastOperation: nullable(o.lastOperation, parseOperation), progress: nullable(o.progress, parseProgress), lastFailure: nullable(o.lastFailure, parseFailure), lastUpdatedAt: dateText(o.lastUpdatedAt) }; }
function parseSource(value: unknown): RagSource { const o = record(value); return { identity: text(o.identity), type: text(o.type), displayValue: text(o.displayValue) }; }
function parseConfiguration(value: unknown): RagIndexConfiguration { const o = record(value); return { ingestionStrategy: enumValue(o.ingestionStrategy, strategyValues), scheduleExpression: optionalText(o.scheduleExpression), vectorStoreType: text(o.vectorStoreType), vectorStoreReference: text(o.vectorStoreReference), embeddingReference: text(o.embeddingReference), chunkSize: count(o.chunkSize), chunkOverlap: count(o.chunkOverlap) }; }
export function parseOperation(value: unknown): RagOperation { const o = record(value); return { operationId: text(o.operationId), indexName: text(o.indexName), reason: enumValue(o.reason, operationReasonValues), state: enumValue(o.state, operationStateValues), startedAt: dateText(o.startedAt), completedAt: optionalDateText(o.completedAt), durationMilliseconds: finite(o.durationMilliseconds), progress: parseProgress(o.progress) }; }
function parseProgress(value: unknown): RagProgress { const o = record(value); return { discoveredDocuments: count(o.discoveredDocuments), processedDocuments: count(o.processedDocuments), addedDocuments: count(o.addedDocuments), updatedDocuments: count(o.updatedDocuments), skippedDocuments: count(o.skippedDocuments), deletedDocuments: count(o.deletedDocuments), failedDocuments: count(o.failedDocuments), producedChunks: count(o.producedChunks), producedEmbeddings: count(o.producedEmbeddings), currentSourceIdentity: optionalText(o.currentSourceIdentity), currentDocumentIdentity: optionalText(o.currentDocumentIdentity) }; }
function parseFailure(value: unknown): RagFailure { const o = record(value); return { code: text(o.code), message: text(o.message), sourceIdentity: optionalText(o.sourceIdentity), documentIdentity: optionalText(o.documentIdentity), timestamp: dateText(o.timestamp) }; }
function parseManagementError(value: unknown): RagManagementError | null { try { const o = record(value); return { code: text(o.code), message: text(o.message), activeOperation: nullable(o.activeOperation, parseOperation) }; } catch { return null; } }
function record(value: unknown): Record<string, unknown> { if (!value || typeof value !== 'object' || Array.isArray(value)) throw new Error(); return value as Record<string, unknown>; }
function array(value: unknown): unknown[] { if (!Array.isArray(value)) throw new Error(); return value; }
function text(value: unknown): string { if (typeof value !== 'string' || !value.trim()) throw new Error(); return value; }
function optionalText(value: unknown): string | null { return value === null || value === undefined ? null : text(value); }
function finite(value: unknown): number { if (typeof value !== 'number' || !Number.isFinite(value)) throw new Error(); return value; }
function count(value: unknown): number { const number = finite(value); if (number < 0 || !Number.isInteger(number)) throw new Error(); return number; }
function dateText(value: unknown): string { const result = text(value); if (Number.isNaN(Date.parse(result))) throw new Error(); return result; }
function optionalDateText(value: unknown): string | null { return value === null || value === undefined ? null : dateText(value); }
function enumValue<T extends string>(value: unknown, values: readonly T[]): T { const result = text(value); if (!values.includes(result as T)) throw new Error(); return result as T; }
function nullable<T>(value: unknown, parse: (input: unknown) => T): T | null { return value === null || value === undefined ? null : parse(value); }

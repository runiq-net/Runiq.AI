import type { RagIndexListItem, RagOperationReason, RagOperationState, RagReadiness, RagRuntimeStatus } from '../api/ragApi';

export const readinessLabels: Record<RagReadiness, string> = { NotInitialized: 'Not initialized', Initializing: 'Initializing', Ready: 'Ready', Degraded: 'Degraded', Failed: 'Failed' };
export const operationStateLabels: Record<RagOperationState, string> = { Pending: 'Pending', Running: 'Running', Cancelling: 'Cancelling', Completed: 'Completed', PartiallyCompleted: 'Partially completed', Failed: 'Failed', Cancelled: 'Cancelled' };
export const operationReasonLabels: Record<RagOperationReason, string> = { Manual: 'Manual', Startup: 'Startup', BackgroundStartup: 'Background startup', Scheduled: 'Scheduled' };

export function summarizeIndexes(indexes: RagIndexListItem[]) {
  return {
    total: indexes.length,
    ready: indexes.filter((index) => index.readiness === 'Ready').length,
    initializing: indexes.filter((index) => index.readiness === 'Initializing').length,
    degraded: indexes.filter((index) => index.readiness === 'Degraded').length,
    failed: indexes.filter((index) => index.readiness === 'Failed').length,
    active: indexes.filter((index) => index.activeOperation !== null).length,
  };
}

export function mergeRuntime(indexes: RagIndexListItem[], status: RagRuntimeStatus): RagIndexListItem[] {
  return indexes.map((index) => index.name === status.indexName ? { ...index, readiness: status.readiness, activeOperation: status.activeOperation, lastOperation: status.lastOperation } : index);
}

export function progressValue(processed: number, discovered: number): number | null {
  if (discovered <= 0) return null;
  return Math.min(100, Math.max(0, (processed / discovered) * 100));
}

export function pollingDelay(status: RagRuntimeStatus | null, visible: boolean): number | null {
  if (!visible) return null;
  return status?.activeOperation ? 1500 : 15000;
}

export function shouldApplyStatus(responseSequence: number, latestSequence: number): boolean {
  return responseSequence === latestSequence;
}

export function formatDuration(milliseconds: number): string {
  if (!Number.isFinite(milliseconds) || milliseconds < 0) return '—';
  if (milliseconds < 1000) return `${Math.round(milliseconds)} ms`;
  if (milliseconds < 60000) return `${(milliseconds / 1000).toFixed(milliseconds % 1000 === 0 ? 0 : 1)} s`;
  const minutes = Math.floor(milliseconds / 60000);
  return `${minutes}m ${Math.floor((milliseconds % 60000) / 1000)}s`;
}

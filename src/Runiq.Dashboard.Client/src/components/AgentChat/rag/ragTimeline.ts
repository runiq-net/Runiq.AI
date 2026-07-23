import type {
  AgentChatRagLifecycle,
  AgentChatStreamEvent,
} from '../../../types/agentChat.ts';

export function applyRagStreamEvent(
  lifecycles: readonly AgentChatRagLifecycle[],
  event: AgentChatStreamEvent,
): AgentChatRagLifecycle[] {
  const lifecycle = toLifecycle(event);

  if (!lifecycle) {
    return [...lifecycles];
  }

  const existingIndex = lifecycles.findIndex(
    (item) => item.payload.correlationId === lifecycle.payload.correlationId,
  );

  if (existingIndex < 0) {
    return [...lifecycles, lifecycle];
  }

  return lifecycles.map((item, index) =>
    index === existingIndex ? lifecycle : item,
  );
}

export function formatRagDuration(value: string | undefined): string {
  if (!value) {
    return '—';
  }

  const match = /^(?:(\d+)\.)?(\d{2}):(\d{2}):(\d{2})(?:\.(\d{1,7}))?$/.exec(value);

  if (!match) {
    return value;
  }

  const [, days = '0', hours, minutes, seconds, fraction = '0'] = match;
  const milliseconds = Number(fraction.padEnd(7, '0').slice(0, 3));
  const totalMilliseconds =
    (((Number(days) * 24 + Number(hours)) * 60 + Number(minutes)) * 60 + Number(seconds)) * 1000 +
    milliseconds;

  if (totalMilliseconds < 1000) {
    return `${totalMilliseconds} ms`;
  }

  if (totalMilliseconds < 60_000) {
    return `${(totalMilliseconds / 1000).toFixed(totalMilliseconds % 1000 === 0 ? 0 : 1)} s`;
  }

  const totalMinutes = Math.floor(totalMilliseconds / 60_000);
  const remainingSeconds = Math.floor((totalMilliseconds % 60_000) / 1000);
  return `${totalMinutes}m ${remainingSeconds}s`;
}

export function getNoContextLabel(reason: string): string {
  return RAG_NO_CONTEXT_LABELS[reason] ?? reason;
}

export function getRejectionReasonLabel(reason: string): string {
  return RAG_REJECTION_REASON_LABELS[reason] ?? reason;
}

export function getFailureClassificationLabel(classification: string): string {
  return RAG_FAILURE_LABELS[classification] ?? classification;
}

export function getDistinctEffectiveQuery(
  originalQuery: string,
  effectiveQuery: string | undefined,
): string | undefined {
  return effectiveQuery && effectiveQuery !== originalQuery
    ? effectiveQuery
    : undefined;
}

function toLifecycle(event: AgentChatStreamEvent): AgentChatRagLifecycle | null {
  switch (event.type) {
    case 'rag_search_started':
      return { status: 'running', payload: event.ragSearch };
    case 'rag_search_completed':
      return { status: 'completed', payload: event.ragSearch };
    case 'rag_search_failed':
      return { status: 'failed', payload: event.ragSearch };
    case 'rag_search_blocked':
      return { status: 'blocked', payload: event.ragSearch };
    default:
      return null;
  }
}

const RAG_NO_CONTEXT_LABELS: Record<string, string> = {
  NoResults: 'No results',
  BelowRelevanceThreshold: 'Below relevance threshold',
  CandidatesRejected: 'Candidates rejected',
};

const RAG_REJECTION_REASON_LABELS: Record<string, string> = {
  DuplicateContent: 'Duplicate content',
  InvalidScore: 'Invalid score',
  BelowMinimumRelevance: 'Below minimum relevance',
  ResultLimitExceeded: 'Result limit exceeded',
  UnsupportedScoreMetric: 'Unsupported score metric',
};

const RAG_FAILURE_LABELS: Record<string, string> = {
  InvalidRequest: 'Invalid request',
  RetrievalFailed: 'Retrieval failed',
  EmbeddingFailed: 'Embedding failed',
  VectorStoreQueryFailed: 'Vector store query failed',
};

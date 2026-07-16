export type AgentChatMethod = 'result' | 'stream';

export type AgentChatMessageRole = 'user' | 'assistant' | 'error';

export type AgentToolCallStatus = 'running' | 'completed' | 'failed';

export type AgentToolCall = {
  id: string;
  name: string;
  status: AgentToolCallStatus;
  argumentsJson?: string;
  outputJson?: string;
  errorCode?: string;
  errorMessage?: string;
};

export type AgentChatMessage = {
  id: string;
  role: AgentChatMessageRole;
  content: string;
  toolCalls?: AgentToolCall[];
  ragSearches?: AgentChatRagLifecycle[];
  isStreaming?: boolean;
};

export type AgentChatStreamEventType =
  | 'assistant_delta'
  | 'tool_call_started'
  | 'tool_call_completed'
  | 'tool_call_failed'
  | 'rag_search_started'
  | 'rag_search_completed'
  | 'rag_search_failed'
  | 'completed'
  | 'failed';

export type AgentChatNonRagStreamEventType = Exclude<
  AgentChatStreamEventType,
  'rag_search_started' | 'rag_search_completed' | 'rag_search_failed'
>;

export type AgentChatRagNoContextReason =
  | 'NoResults'
  | 'BelowRelevanceThreshold'
  | 'CandidatesRejected';

export type AgentChatRagFailureClassification =
  | 'InvalidRequest'
  | 'RetrievalFailed'
  | 'EmbeddingFailed'
  | 'VectorStoreQueryFailed';

export type AgentChatRagRejectionReason =
  | 'DuplicateContent'
  | 'InvalidScore'
  | 'BelowMinimumRelevance'
  | 'ResultLimitExceeded'
  | 'UnsupportedScoreMetric';

export type AgentChatRagSelectedResult = {
  documentId: string;
  chunkId: string;
};

export type AgentChatRagRejectedResult = {
  documentId: string;
  chunkId: string;
  rawScore?: number;
  normalizedRelevance?: number;
  reason: AgentChatRagRejectionReason;
};

export type AgentChatRagSearchEventBase = {
  agentId: string;
  conversationId: string;
  correlationId: string;
  indexName: string;
  originalQuery: string;
  effectiveQuery?: string;
  requestedCandidateCount: number;
};

export type AgentChatRagSearchStartedEvent = AgentChatRagSearchEventBase;

export type AgentChatRagSearchCompletedEvent = AgentChatRagSearchEventBase & {
  actualCandidateCount: number;
  acceptedCount: number;
  rejectedCount: number;
  maximumAcceptedResultCount: number;
  topRawScore?: number;
  topNormalizedRelevance?: number;
  duration: string;
  selectedResults: AgentChatRagSelectedResult[];
  rejectedResults: AgentChatRagRejectedResult[];
  noContextReason?: AgentChatRagNoContextReason;
};

export type AgentChatRagSearchFailedEvent = AgentChatRagSearchEventBase & {
  duration: string;
  failureClassification: AgentChatRagFailureClassification;
};

export type AgentChatRagLifecycle =
  | {
    status: 'running';
    payload: AgentChatRagSearchStartedEvent;
  }
  | {
    status: 'completed';
    payload: AgentChatRagSearchCompletedEvent;
  }
  | {
    status: 'failed';
    payload: AgentChatRagSearchFailedEvent;
  };

export type AgentChatStreamEventFields = {
  content?: string | null;
  toolCallId?: string | null;
  toolName?: string | null;
  argumentsJson?: string | null;
  outputJson?: string | null;
  errorCode?: string | null;
  errorMessage?: string | null;
};

export type AgentChatStreamEvent = AgentChatStreamEventFields & (
  | {
    type: 'rag_search_started';
    ragSearch: AgentChatRagSearchStartedEvent;
  }
  | {
    type: 'rag_search_completed';
    ragSearch: AgentChatRagSearchCompletedEvent;
  }
  | {
    type: 'rag_search_failed';
    ragSearch: AgentChatRagSearchFailedEvent;
  }
  | {
    type: AgentChatNonRagStreamEventType;
    ragSearch?: null;
  }
);

export type AgentChatExecutionStepKind =
  | 'tool_call'
  | 'final_answer'
  | 'error';

export type AgentChatExecutionStepStatus =
  | 'running'
  | 'completed'
  | 'failed';

export type AgentChatExecutionStep = {
  index: number;
  kind: AgentChatExecutionStepKind;
  content?: string | null;
  toolCallId?: string | null;
  toolName?: string | null;
  argumentsJson?: string | null;
  outputJson?: string | null;
  errorCode?: string | null;
  errorMessage?: string | null;
  status: AgentChatExecutionStepStatus;
  startedAt?: string | null;
  completedAt?: string | null;
};

export type AgentChatResult = {
  isSuccess?: boolean;
  message?: string | null;
  errorCode?: string | null;
  errorMessage?: string | null;
  steps?: AgentChatExecutionStep[];
};

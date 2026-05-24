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

export type AgentProvidedContextSpace = {
  id: string;
  name: string;
  description?: string | null;
};

export type AgentProvidedSkill = {
  id: string;
  name: string;
  description?: string | null;
  version?: string | null;
  tags?: string[] | null;
  sourceId?: string | null;
  relativePath?: string | null;
};

export type AgentProvidedSource = {
  id: string;
  name: string;
  kind?: string | null;
  description?: string | null;
};

export type AgentProvidedContext = {
  contextSpaces: AgentProvidedContextSpace[];
  skills: AgentProvidedSkill[];
  sources: AgentProvidedSource[];
};

export type AgentSourceSearchResult = {
  sourceId: string;
  sourceName: string;
  relativePath: string;
  fileName: string;
  snippet: string;
  score: number;
};

export type AgentChatMessage = {
  id: string;
  role: AgentChatMessageRole;
  content: string;
  context?: AgentProvidedContext;
  sourceSearchResults?: AgentSourceSearchResult[];
  toolCalls?: AgentToolCall[];
  isStreaming?: boolean;
};

export type AgentChatStreamEventType =
  | 'context_provided'
  | 'assistant_delta'
  | 'context_searched'
  | 'tool_call_started'
  | 'tool_call_completed'
  | 'tool_call_failed'
  | 'completed'
  | 'failed';

export type AgentChatStreamEvent = {
  type: AgentChatStreamEventType;
  content?: string | null;
  contextSpaces?: AgentProvidedContextSpace[] | null;
  skills?: AgentProvidedSkill[] | null;
  sources?: AgentProvidedSource[] | null;
  sourceSearchResults?: AgentSourceSearchResult[] | null;
  toolCallId?: string | null;
  toolName?: string | null;
  argumentsJson?: string | null;
  outputJson?: string | null;
  errorCode?: string | null;
  errorMessage?: string | null;
};

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
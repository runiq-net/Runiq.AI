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
  isStreaming?: boolean;
};

export type AgentChatStreamEventType =
  | 'assistant_delta'
  | 'tool_call_started'
  | 'tool_call_completed'
  | 'tool_call_failed'
  | 'completed'
  | 'failed';

export type AgentChatStreamEvent = {
  type: AgentChatStreamEventType;
  content?: string | null;
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
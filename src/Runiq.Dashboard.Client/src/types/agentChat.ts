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
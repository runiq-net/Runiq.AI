import type { AgentChatResult, AgentChatStreamEvent } from '../types/agentChat';

type SendAgentMessageRequest = {
  basePath: string;
  agentId: string;
  message: string;
};



function trimTrailingSlash(value: string): string {
  return value.endsWith('/') ? value.slice(0, -1) : value;
}

function buildAgentChatUrl(basePath: string, agentId: string): string {
  return `${trimTrailingSlash(basePath)}/api/agents/${encodeURIComponent(agentId)}/chat`;
}

export async function sendAgentMessage({
  basePath,
  agentId,
  message,
}: SendAgentMessageRequest): Promise<AgentChatResult> {
  const response = await fetch(buildAgentChatUrl(basePath, agentId), {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({
      message,
      responseMode: 'result',
    }),
  });

  const payload = (await response.json()) as AgentChatResult;

  if (!response.ok || payload.isSuccess === false) {
    throw new Error(
      payload.errorMessage ||
      payload.errorCode ||
      `Agent chat request failed. Status: ${response.status}`,
    );
  }

  if (!payload.message) {
    throw new Error('Agent response was empty.');
  }

  return payload;
}

export async function streamAgentMessage(
  { basePath, agentId, message }: SendAgentMessageRequest,
  onEvent: (event: AgentChatStreamEvent) => void,
): Promise<void> {
  const response = await fetch(buildAgentChatUrl(basePath, agentId), {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Accept: 'text/event-stream',
    },
    body: JSON.stringify({
      message,
      responseMode: 'stream',
    }),
  });

  if (!response.ok) {
    const errorText = await response.text();
    throw new Error(errorText || `Agent stream request failed. Status: ${response.status}`);
  }

  if (!response.body) {
    throw new Error('Agent stream response body was empty.');
  }

  const reader = response.body.getReader();
  const decoder = new TextDecoder();

  let buffer = '';

  while (true) {
    const { value, done } = await reader.read();

    if (done) {
      break;
    }

    buffer += decoder.decode(value, { stream: true });

    const events = buffer.split(/\r?\n\r?\n/);
    buffer = events.pop() ?? '';

    for (const event of events) {
      const streamEvent = parseServerSentEvent(event);

      if (streamEvent) {
        onEvent(streamEvent);
      }
    }
  }

  buffer += decoder.decode();

  const finalEvent = parseServerSentEvent(buffer);

  if (finalEvent) {
    onEvent(finalEvent);
  }
}

function parseServerSentEvent(event: string): AgentChatStreamEvent | null {
  const dataLines = event
    .split('\n')
    .map((line) => line.trim())
    .filter((line) => line.startsWith('data:'));

  if (dataLines.length === 0) {
    return null;
  }

  for (const line of dataLines) {
    const data = line.replace(/^data:\s?/, '').trim();

    if (!data || data === '[DONE]') {
      continue;
    }

    const streamEvent = parseStreamEventPayload(data);

    if (streamEvent) {
      return streamEvent;
    }
  }

  return null;
}

export function parseStreamEventPayload(data: string): AgentChatStreamEvent | null {
  try {
    const parsed = JSON.parse(data) as Partial<AgentChatStreamEvent>;

    if (!parsed.type) {
      return null;
    }

    const isKnownRagEvent =
      parsed.type === 'rag_search_started' ||
      parsed.type === 'rag_search_completed' ||
      parsed.type === 'rag_search_failed';

    if (parsed.type.startsWith('rag_search_') && !isKnownRagEvent) {
      return null;
    }

    if (
      isKnownRagEvent &&
      !isValidRagPayload(parsed.type, parsed.ragSearch)
    ) {
      return null;
    }

    return {
      type: parsed.type,
      content: parsed.content ?? null,
      toolCallId: parsed.toolCallId ?? null,
      toolName: parsed.toolName ?? null,
      argumentsJson: parsed.argumentsJson ?? null,
      outputJson: parsed.outputJson ?? null,
      errorCode: parsed.errorCode ?? null,
      errorMessage: parsed.errorMessage ?? null,
      ragSearch: parsed.ragSearch ?? null,
    } as AgentChatStreamEvent;


  } catch {
    return null;
  }
}

function isValidRagPayload(
  type: AgentChatStreamEvent['type'],
  payload: unknown,
): boolean {
  if (!isRecord(payload) ||
    !hasString(payload, 'agentId') ||
    !hasString(payload, 'conversationId') ||
    !hasString(payload, 'correlationId') ||
    !hasString(payload, 'indexName') ||
    !hasString(payload, 'originalQuery') ||
    !hasNumber(payload, 'requestedCandidateCount')) {
    return false;
  }

  if (type === 'rag_search_started') {
    return payload.effectiveQuery === undefined || typeof payload.effectiveQuery === 'string';
  }

  if (type === 'rag_search_failed') {
    return hasString(payload, 'duration') && hasString(payload, 'failureClassification');
  }

  if (type === 'rag_search_completed') {
    return hasNumber(payload, 'actualCandidateCount') &&
      hasNumber(payload, 'acceptedCount') &&
      hasNumber(payload, 'rejectedCount') &&
      hasNumber(payload, 'maximumAcceptedResultCount') &&
      hasString(payload, 'duration') &&
      Array.isArray(payload.selectedResults) &&
      Array.isArray(payload.rejectedResults);
  }

  return true;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null;
}

function hasString(value: Record<string, unknown>, key: string): boolean {
  return typeof value[key] === 'string';
}

function hasNumber(value: Record<string, unknown>, key: string): boolean {
  return typeof value[key] === 'number' && Number.isFinite(value[key]);
}

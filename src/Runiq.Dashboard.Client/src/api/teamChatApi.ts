export type TeamChatStreamEvent = {
  type:
    | 'team_started'
    | 'member_started'
    | 'member_delta'
    | 'member_tool_call_started'
    | 'member_tool_call_completed'
    | 'member_tool_call_failed'
    | 'member_completed'
    | 'member_failed'
    | 'team_completed'
    | 'team_failed';
  content?: string | null;
  teamId: string;
  teamName?: string | null;
  memberAgentId?: string | null;
  memberRole?: string | null;
  toolCallId?: string | null;
  toolName?: string | null;
  argumentsJson?: string | null;
  outputJson?: string | null;
  errorCode?: string | null;
  errorMessage?: string | null;
  isFinalMember?: boolean | null;
};

type StreamTeamMessageRequest = {
  basePath: string;
  teamId: string;
  message: string;
};

export async function streamTeamMessage(
  request: StreamTeamMessageRequest,
  onEvent: (event: TeamChatStreamEvent) => void,
): Promise<void> {
  const response = await fetch(
    `${request.basePath}/api/teams/${encodeURIComponent(request.teamId)}/chat`,
    {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({
        message: request.message,
      }),
    },
  );

  if (!response.ok) {
    throw new Error(`Team execution failed with status ${response.status}.`);
  }

  if (!response.body) {
    throw new Error('Team execution stream was empty.');
  }

  const reader = response.body.getReader();
  const decoder = new TextDecoder();
  let buffer = '';

  while (true) {
    const result = await reader.read();

    if (result.done) {
      break;
    }

    buffer += decoder.decode(result.value, { stream: true });

    const lines = buffer.split('\n');
    buffer = lines.pop() ?? '';

    for (const line of lines) {
      const trimmedLine = line.trim();

      if (!trimmedLine.startsWith('data: ')) {
        continue;
      }

      const payload = trimmedLine.slice('data: '.length);

      if (payload === '[DONE]') {
        return;
      }

      onEvent(JSON.parse(payload) as TeamChatStreamEvent);
    }
  }
}

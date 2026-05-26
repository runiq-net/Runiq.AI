import { useEffect, useState } from 'react';

import { getTeams, type TeamMetadata } from '../api/agentMetadataApi';
import { streamTeamMessage, type TeamChatStreamEvent } from '../api/teamChatApi';
import { ChatComposer } from '../components/AgentChat/ChatComposer';
import { ChatThread } from '../components/AgentChat/ChatThread';
import { TeamInspectorPanel } from '../components/TeamChat/TeamInspectorPanel';
import { getDashboardBasePath } from '../dashboardConfig';
import type { AgentChatMessage } from '../types/agentChat';

type TeamChatPageProps = {
  teamId: string;
};

function createMessage(
  role: AgentChatMessage['role'],
  content: string,
): AgentChatMessage {
  return {
    id:
      typeof crypto !== 'undefined' && 'randomUUID' in crypto
        ? crypto.randomUUID()
        : `${Date.now()}-${Math.random()}`,
    role,
    content,
  };
}

export function TeamChatPage({ teamId }: TeamChatPageProps) {
  const [team, setTeam] = useState<TeamMetadata | null>(null);
  const [isLoading, setLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [messages, setMessages] = useState<AgentChatMessage[]>([]);
  const [isExecuting, setExecuting] = useState(false);

  useEffect(() => {
    let isMounted = true;

    async function loadTeam() {
      try {
        setLoading(true);
        setErrorMessage(null);

        const teams = await getTeams(getDashboardBasePath());
        const selectedTeam =
          teams.find((item) => item.id.toLowerCase() === teamId.toLowerCase()) ??
          null;

        if (!isMounted) {
          return;
        }

        if (!selectedTeam) {
          setTeam(null);
          setErrorMessage(`Agent team '${teamId}' could not be found.`);
          return;
        }

        setTeam(selectedTeam);
      } catch (error) {
        if (!isMounted) {
          return;
        }

        setTeam(null);
        setErrorMessage(
          error instanceof Error
            ? error.message
            : 'Agent team metadata could not be loaded.',
        );
      } finally {
        if (isMounted) {
          setLoading(false);
        }
      }
    }

    void loadTeam();

    return () => {
      isMounted = false;
    };
  }, [teamId]);

  async function handleSubmit(message: string) {
    const basePath = getDashboardBasePath();

    const userMessage = createMessage('user', message);
    const assistantMessage: AgentChatMessage = {
      ...createMessage('assistant', ''),
      isStreaming: true,
      toolCalls: [],
    };

    setMessages((current) => [...current, userMessage, assistantMessage]);
    setExecuting(true);

    try {
      await streamTeamMessage(
        {
          basePath,
          teamId,
          message,
        },
        (event) => {
          setMessages((current) =>
            current.map((item) =>
              item.id === assistantMessage.id
                ? applyTeamStreamEvent(item, event)
                : item,
            ),
          );
        },
      );

      setMessages((current) =>
        current.map((item) =>
          item.id === assistantMessage.id
            ? { ...item, isStreaming: false }
            : item,
        ),
      );
    } catch (error) {
      setMessages((current) =>
        current.map((item) =>
          item.id === assistantMessage.id
            ? {
                ...item,
                role: 'error',
                content:
                  error instanceof Error
                    ? error.message
                    : 'Agent team execution failed.',
                isStreaming: false,
              }
            : item,
        ),
      );
    } finally {
      setExecuting(false);
    }
  }

  if (isLoading) {
    return (
      <div className="flex h-full min-h-0 w-full items-center justify-center rounded-lg border border-zinc-200 bg-white text-sm text-zinc-500 shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40 dark:text-zinc-500 dark:shadow-none">
        Loading agent team metadata...
      </div>
    );
  }

  if (errorMessage || !team) {
    return (
      <div className="flex h-full min-h-0 w-full items-center justify-center rounded-lg border border-red-200 bg-red-50 px-6 text-center text-sm text-red-700 shadow-sm dark:border-red-900/60 dark:bg-red-950/20 dark:text-red-300 dark:shadow-none">
        {errorMessage ?? 'Agent team metadata could not be loaded.'}
      </div>
    );
  }

  return (
    <div className="flex h-full min-h-0 w-full gap-3">
      <section className="flex min-w-0 flex-1 flex-col gap-2.5">
        <ChatThread messages={messages} isWaiting={false} />

        <ChatComposer disabled={isExecuting} onSubmit={handleSubmit} />
      </section>

      <TeamInspectorPanel team={team} />
    </div>
  );
}

function applyTeamStreamEvent(
  message: AgentChatMessage,
  event: TeamChatStreamEvent,
): AgentChatMessage {
  if (event.type === 'member_delta') {
    return {
      ...message,
      content: message.content + (event.content ?? ''),
      isStreaming: true,
    };
  }

  if (event.type === 'team_completed') {
    return {
      ...message,
      content: event.content?.trim() ? event.content : message.content,
      isStreaming: false,
    };
  }

  if (event.type === 'team_failed') {
    return {
      ...message,
      role: 'error',
      content:
        event.errorMessage ??
        event.content ??
        'Agent team execution failed.',
      isStreaming: false,
    };
  }

  if (event.type === 'member_failed') {
    return {
      ...message,
      role: 'error',
      content:
        event.errorMessage ??
        event.content ??
        `Team member '${event.memberAgentId ?? event.memberRole ?? 'unknown'}' failed.`,
      isStreaming: false,
    };
  }

  return {
    ...message,
    isStreaming: true,
  };
}
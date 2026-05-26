import { useEffect, useState } from 'react';

import { getTeams, type TeamMetadata } from '../api/agentMetadataApi';
import { streamTeamMessage, type TeamChatStreamEvent } from '../api/teamChatApi';
import { ChatComposer } from '../components/AgentChat/ChatComposer';
import { ChatThread } from '../components/AgentChat/ChatThread';
import { TeamInspectorPanel } from '../components/TeamChat/TeamInspectorPanel';
import { getDashboardBasePath } from '../dashboardConfig';
import type {
    AgentChatMessage,
    AgentTeamStep,
    AgentToolCall,
} from '../types/agentChat';

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
            teamSteps: [],
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
    if (event.type === 'member_started') {
        return {
            ...message,
            teamSteps: upsertTeamStep(message.teamSteps ?? [], {
                id: createTeamStepId(event),
                agentId: event.memberAgentId ?? 'unknown',
                role: event.memberRole ?? 'Member',
                status: 'running',
            }),
            isStreaming: true,
        };
    }

    if (event.type === 'member_delta') {
        return {
            ...message,
            content: event.isFinalMember === true
                ? message.content + (event.content ?? '')
                : message.content,
            teamSteps: appendTeamStepContent(
                message.teamSteps ?? [],
                createTeamStepId(event),
                event.content ?? '',
            ),
            isStreaming: true,
        };
    }

    if (event.type === 'member_tool_call_started') {
        return {
            ...message,
            teamSteps: updateTeamStepToolCall(message.teamSteps ?? [], event, {
                id: event.toolCallId ?? createFallbackToolCallId(event),
                name: event.toolName ?? 'unknown',
                status: 'running',
                argumentsJson: event.argumentsJson ?? undefined,
            }),
            isStreaming: true,
        };
    }

    if (event.type === 'member_tool_call_completed') {
        return {
            ...message,
            teamSteps: updateTeamStepToolCall(message.teamSteps ?? [], event, {
                id: event.toolCallId ?? createFallbackToolCallId(event),
                name: event.toolName ?? 'unknown',
                status: 'completed',
                outputJson: event.outputJson ?? undefined,
            }),
            isStreaming: true,
        };
    }

    if (event.type === 'member_tool_call_failed') {
        return {
            ...message,
            teamSteps: updateTeamStepToolCall(message.teamSteps ?? [], event, {
                id: event.toolCallId ?? createFallbackToolCallId(event),
                name: event.toolName ?? 'unknown',
                status: 'failed',
                errorCode: event.errorCode ?? undefined,
                errorMessage: event.errorMessage ?? undefined,
            }),
            isStreaming: true,
        };
    }

    if (event.type === 'member_completed') {
        return {
            ...message,
            teamSteps: upsertTeamStep(message.teamSteps ?? [], {
                id: createTeamStepId(event),
                agentId: event.memberAgentId ?? 'unknown',
                role: event.memberRole ?? 'Member',
                status: 'completed',
                content: event.content ?? undefined,
            }),
            isStreaming: true,
        };
    }

    if (event.type === 'member_failed') {
        const memberErrorMessage =
            event.errorMessage ??
            event.content ??
            `Team member '${event.memberAgentId ?? event.memberRole ?? 'unknown'}' failed.`;

        return {
            ...message,
            role: 'error',
            content: memberErrorMessage,
            teamSteps: upsertTeamStep(message.teamSteps ?? [], {
                id: createTeamStepId(event),
                agentId: event.memberAgentId ?? 'unknown',
                role: event.memberRole ?? 'Member',
                status: 'failed',
                content: event.content ?? undefined,
                errorCode: event.errorCode ?? undefined,
                errorMessage: memberErrorMessage,
            }),
            isStreaming: false,
        };
    }

    if (event.type === 'team_completed') {
        const completedContent = event.content ?? '';

        return {
            ...message,
            content: shouldUseTeamCompletedContent(message, completedContent)
                ? completedContent
                : message.content,
            isStreaming: false,
        };
    }

    if (event.type === 'team_failed') {
        if (message.role === 'error' && message.content.trim().length > 0) {
            return {
                ...message,
                isStreaming: false,
            };
        }

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

    return {
        ...message,
        isStreaming: true,
    };
}

function createTeamStepId(event: TeamChatStreamEvent): string {
  return `${event.memberAgentId ?? 'unknown'}:${event.memberRole ?? 'Member'}`;
}

function upsertTeamStep(
  steps: AgentTeamStep[],
  nextStep: AgentTeamStep,
): AgentTeamStep[] {
  const existingIndex = steps.findIndex((step) => step.id === nextStep.id);

  if (existingIndex < 0) {
    return [...steps, nextStep];
  }

  return steps.map((step, index) =>
    index === existingIndex
      ? {
          ...step,
          ...nextStep,
          content: nextStep.content ?? step.content,
        }
      : step,
  );
}

function appendTeamStepContent(
  steps: AgentTeamStep[],
  stepId: string,
  content: string,
): AgentTeamStep[] {
  if (!content) {
    return steps;
  }

  return steps.map((step) =>
    step.id === stepId
      ? {
          ...step,
          content: `${step.content ?? ''}${content}`,
        }
      : step,
  );
}

function updateTeamStepToolCall(
  steps: AgentTeamStep[],
  event: TeamChatStreamEvent,
  toolCall: AgentToolCall,
): AgentTeamStep[] {
  const stepId = createTeamStepId(event);
  const baseStep: AgentTeamStep = {
    id: stepId,
    agentId: event.memberAgentId ?? 'unknown',
    role: event.memberRole ?? 'Member',
    status: 'running',
  };

  const existingStep = steps.find((step) => step.id === stepId);
  const nextStep = existingStep ?? baseStep;

  return upsertTeamStep(steps, {
    ...nextStep,
    toolCalls: upsertToolCall(nextStep.toolCalls ?? [], toolCall),
  });
}

function upsertToolCall(
  toolCalls: AgentToolCall[],
  nextToolCall: AgentToolCall,
): AgentToolCall[] {
  const existingIndex = toolCalls.findIndex(
    (toolCall) => toolCall.id === nextToolCall.id,
  );

  if (existingIndex < 0) {
    return [...toolCalls, nextToolCall];
  }

  return toolCalls.map((toolCall, index) =>
    index === existingIndex
      ? {
          ...toolCall,
          ...nextToolCall,
          argumentsJson: nextToolCall.argumentsJson ?? toolCall.argumentsJson,
          outputJson: nextToolCall.outputJson ?? toolCall.outputJson,
          errorCode: nextToolCall.errorCode ?? toolCall.errorCode,
          errorMessage: nextToolCall.errorMessage ?? toolCall.errorMessage,
        }
      : toolCall,
  );
}

function createFallbackToolCallId(event: TeamChatStreamEvent): string {
  return `${createTeamStepId(event)}:${event.toolName ?? 'tool'}`;
}

function shouldUseTeamCompletedContent(
  message: AgentChatMessage,
  content: string,
): boolean {
  if (message.content.trim().length > 0) {
    return false;
  }

  if (content.trim().length === 0) {
    return false;
  }

  return !looksLikeToolTraceContent(content);
}

function looksLikeToolTraceContent(content: string): boolean {
  const trimmed = content.trim();

  return (
    trimmed.startsWith('Tool:') ||
    trimmed.startsWith('Output:') ||
    trimmed.startsWith('{') ||
    trimmed.startsWith('[')
  );
}

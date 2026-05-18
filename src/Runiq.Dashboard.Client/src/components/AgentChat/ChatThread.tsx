import { useEffect, useRef, useState } from 'react';

import './ChatThread.css';

import type { AgentChatMessage, AgentToolCall } from '../../types/agentChat';
import { TypingIndicator } from './TypingIndicator';

type ChatThreadProps = {
  messages: AgentChatMessage[];
  isWaiting: boolean;
};

export function ChatThread({ messages, isWaiting }: ChatThreadProps) {
  const bottomRef = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ block: 'end' });
  }, [messages, isWaiting]);

  return (
    <section className="flex min-h-0 flex-1 flex-col rounded-lg border border-zinc-200 bg-white shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40 dark:shadow-none">
      {messages.length === 0 && !isWaiting ? (
        <div className="flex min-h-0 flex-1 items-center justify-center px-6 py-10">
          <div className="max-w-md text-center">
            <div className="text-sm font-medium text-zinc-950 dark:text-zinc-100">
              Start a conversation with this agent
            </div>

            <p className="mt-2 text-sm leading-6 text-zinc-600 dark:text-zinc-500">
              Messages will appear here.
            </p>
          </div>
        </div>
      ) : (
        <div className="agent-chat-scroll flex min-h-0 flex-1 flex-col gap-4 overflow-y-auto px-4 py-5">
          {messages.map((message) => (
            <ChatMessageItem key={message.id} message={message} />
          ))}

          {isWaiting && (
            <article className="mr-auto w-fit max-w-[min(860px,82%)] rounded-2xl border border-zinc-200 bg-white px-5 py-3 text-sm leading-7 text-zinc-500 shadow-sm dark:border-zinc-800 dark:bg-zinc-900/60 dark:text-zinc-400 dark:shadow-none">
              <TypingIndicator />
            </article>
          )}

          <div ref={bottomRef} />
        </div>
      )}
    </section>
  );
}

function ChatMessageItem({ message }: { message: AgentChatMessage }) {
  const hasToolCalls = Boolean(message.toolCalls?.length);
  const hasContent = Boolean(message.content.trim());

  return (
    <article
      className={[
        'max-w-[78%] rounded-2xl px-4 py-3 text-sm leading-6',
        message.role === 'user'
          ? 'ml-auto w-fit max-w-[70%] bg-zinc-950 text-white dark:bg-zinc-100 dark:text-zinc-950'
          : '',
        message.role === 'assistant'
          ? 'mr-auto w-fit max-w-[min(860px,82%)] border border-zinc-200 bg-white text-zinc-900 shadow-sm dark:border-zinc-800 dark:bg-zinc-900/60 dark:text-zinc-100 dark:shadow-none'
          : '',
        message.role === 'error'
          ? 'mr-auto w-fit max-w-[min(860px,82%)] border border-red-200 bg-red-50 text-red-700 dark:border-red-900/60 dark:bg-red-950/20 dark:text-red-300'
          : '',
      ].join(' ')}
    >
      {hasToolCalls && (
        <div className="mb-3 flex flex-col gap-2">
          {message.toolCalls?.map((toolCall) => (
            <ToolCallCard key={toolCall.id} toolCall={toolCall} />
          ))}
        </div>
      )}

      {hasContent && (
        <div className="whitespace-pre-wrap break-words">
          {message.content}
        </div>
      )}

      {!hasContent && !hasToolCalls && <TypingIndicator />}
    </article>
  );
}

function ToolCallCard({ toolCall }: { toolCall: AgentToolCall }) {
  const [isOpen, setIsOpen] = useState(false);

  return (
    <div className="overflow-hidden rounded-xl border border-zinc-200 bg-zinc-50 text-xs dark:border-zinc-800 dark:bg-zinc-950/70">
      <button
        type="button"
        onClick={() => setIsOpen((current) => !current)}
        className="flex w-full items-center gap-2 px-3 py-2 text-left"
      >
        <span className="text-zinc-400 dark:text-zinc-500">
          {isOpen ? '⌄' : '›'}
        </span>

        <span className="inline-flex size-2 rounded-full bg-amber-400" />

        <span className="font-mono text-sm font-semibold text-zinc-900 dark:text-zinc-100">
          {toolCall.name}
        </span>

        <span className="ml-auto rounded-full border border-zinc-200 px-2 py-0.5 text-[11px] font-medium text-zinc-500 dark:border-zinc-700 dark:text-zinc-400">
          {formatToolStatus(toolCall.status)}
        </span>
      </button>

      {isOpen && (
        <div className="space-y-3 border-t border-zinc-200 px-3 py-3 dark:border-zinc-800">
          <ToolJsonBlock
            title="Tool arguments"
            value={toolCall.argumentsJson}
            emptyText="No arguments."
          />

          {toolCall.status === 'completed' && (
            <ToolJsonBlock
              title="Tool result"
              value={toolCall.outputJson}
              emptyText="No result."
            />
          )}

          {toolCall.status === 'failed' && (
            <div>
              <div className="mb-1 text-xs font-semibold text-zinc-900 dark:text-zinc-100">
                Tool error
              </div>

              <pre className="max-h-56 overflow-auto rounded-lg border border-red-200 bg-red-50 p-3 text-xs leading-5 text-red-700 dark:border-red-900/60 dark:bg-red-950/20 dark:text-red-300">
                {toolCall.errorMessage ?? 'Tool execution failed.'}
              </pre>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

function ToolJsonBlock({
  title,
  value,
  emptyText,
}: {
  title: string;
  value?: string;
  emptyText: string;
}) {
  return (
    <div>
      <div className="mb-1 text-xs font-semibold text-zinc-900 dark:text-zinc-100">
        {title}
      </div>

      <pre className="max-h-56 overflow-auto rounded-lg border border-zinc-200 bg-white p-3 text-xs leading-5 text-zinc-700 dark:border-zinc-800 dark:bg-zinc-900 dark:text-zinc-300">
        {value ? formatJson(value) : emptyText}
      </pre>
    </div>
  );
}

function formatJson(value: string): string {
  try {
    return JSON.stringify(JSON.parse(value), null, 2);
  } catch {
    return value;
  }
}

function formatToolStatus(status: AgentToolCall['status']): string {
  if (status === 'running') {
    return 'Running';
  }

  if (status === 'completed') {
    return 'Completed';
  }

  return 'Failed';
}
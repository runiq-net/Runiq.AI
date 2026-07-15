import { useEffect, useRef, useState } from 'react';
import { Check, Copy } from 'lucide-react';

import './ChatThread.css';

import { ToolCallCard } from './tool/ToolCallCard';

import type { AgentChatMessage } from '../../types/agentChat';

type ChatThreadProps = {
  messages: AgentChatMessage[];
  isWaiting: boolean;
};

export function ChatThread({ messages, isWaiting }: ChatThreadProps) {
  const bottomRef = useRef<HTMLDivElement | null>(null);

  const isWritingResponse = messages.some(
    (message) =>
      message.role === 'assistant' &&
      message.isStreaming === true &&
      message.content.trim().length > 0,
  );

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ block: 'end' });
  }, [messages, isWaiting]);

  return (
    <section className="relative flex min-h-0 flex-1 flex-col rounded-lg border border-zinc-200 bg-white shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40 dark:shadow-none">
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
            <article className="mr-auto w-full max-w-[min(760px,82%)] text-sm leading-7 text-zinc-500 dark:text-zinc-400">
              <DotsOnlyIndicator />
            </article>
          )}

          <div ref={bottomRef} />
        </div>
      )}

      {isWritingResponse && (
        <div className="pointer-events-none absolute bottom-5 left-1/2 z-10 -translate-x-1/2">
          <FloatingWritingIndicator />
        </div>
      )}
    </section>
  );
}

function ChatMessageItem({ message }: { message: AgentChatMessage }) {
  const [copied, setCopied] = useState(false);

  const hasToolCalls = Boolean(message.toolCalls?.length);

  const hasContent = Boolean(message.content.trim());

  const isAssistantStreaming =
    message.role === 'assistant' && message.isStreaming === true;

  const showInitialThinking =
    message.role === 'assistant' &&
    isAssistantStreaming &&
    !hasToolCalls;


  const showToolWaiting =
    message.role === 'assistant' &&
    isAssistantStreaming &&
    !hasContent &&
    hasToolCalls;

  const showCopy =
    message.role === 'assistant' &&
    hasContent &&
    !isAssistantStreaming;

  async function handleCopyAnswer() {
    if (!showCopy) {
      return;
    }

    try {
      await navigator.clipboard.writeText(message.content);
      setCopied(true);
      window.setTimeout(() => setCopied(false), 1200);
    } catch {
      setCopied(false);
    }
  }

  if (message.role === 'user') {
    return (
      <article className="ml-auto w-fit max-w-[70%] rounded-2xl bg-zinc-950 px-4 py-3 text-sm leading-6 text-white dark:bg-zinc-100 dark:text-zinc-950">
        <div className="whitespace-pre-wrap break-words">
          {message.content}
        </div>
      </article>
    );
  }

  if (message.role === 'error') {
    return (
      <article className="mr-auto w-full max-w-[min(760px,82%)] rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm leading-6 text-red-700 dark:border-red-900/60 dark:bg-red-950/20 dark:text-red-300">
        <div className="whitespace-pre-wrap break-words">
          {message.content}
        </div>
      </article>
    );
  }

  return (
    <article className="mr-auto w-full max-w-[min(760px,82%)] text-sm leading-6 text-zinc-900 dark:text-zinc-100">

      {hasToolCalls && (
        <div className="mb-4 flex flex-col gap-2">
          {message.toolCalls?.map((toolCall) => (
            <ToolCallCard
              key={toolCall.id}
              toolCall={toolCall}
              forceWorking={isAssistantStreaming && !hasContent}
            />
          ))}
        </div>
      )}

      {showInitialThinking && (
        <div className="mt-2">
          <DotsOnlyIndicator />
        </div>
      )}

      {showToolWaiting && (
        <div className="mt-2">
          <DotsOnlyIndicator />
        </div>
      )}

      {hasContent && (
        <div className="group">
          <div className="whitespace-pre-wrap break-words">
            {message.content}
          </div>

          {showCopy && (
            <div className="mt-3 flex items-center gap-2 opacity-0 transition group-hover:opacity-100">
              <button
                type="button"
                onClick={handleCopyAnswer}
                className="inline-flex size-8 items-center justify-center rounded-lg border border-zinc-200 text-zinc-500 transition hover:bg-zinc-100 hover:text-zinc-950 dark:border-zinc-800 dark:text-zinc-400 dark:hover:bg-zinc-900 dark:hover:text-zinc-100"
                aria-label="Copy assistant answer"
                title={copied ? 'Copied' : 'Copy'}
              >
                {copied ? (
                  <Check className="size-4" />
                ) : (
                  <Copy className="size-4" />
                )}
              </button>

              {copied && (
                <span className="text-xs text-zinc-500 dark:text-zinc-400">
                  Copied
                </span>
              )}
            </div>
          )}
        </div>
      )}
    </article>
  );
}

function DotsOnlyIndicator() {
  return (
    <div className="inline-flex items-center gap-1.5 rounded-full border border-zinc-200 bg-white px-3 py-2 text-zinc-500 shadow-sm dark:border-zinc-800 dark:bg-zinc-950/70 dark:text-zinc-400">
      <AnimatedDots />
    </div>
  );
}

function FloatingWritingIndicator() {
  return (
    <div className="inline-flex items-center gap-2 rounded-full border border-zinc-200 bg-white/95 px-3 py-2 text-xs text-zinc-500 shadow-lg backdrop-blur dark:border-zinc-800 dark:bg-zinc-950/90 dark:text-zinc-400">
      <AnimatedDots />
    </div>
  );
}

function AnimatedDots() {
  return (
    <span className="inline-flex items-center gap-1">
      <span className="size-1.5 animate-bounce rounded-full bg-current [animation-delay:-0.2s]" />
      <span className="size-1.5 animate-bounce rounded-full bg-current [animation-delay:-0.1s]" />
      <span className="size-1.5 animate-bounce rounded-full bg-current" />
    </span>
  );
}

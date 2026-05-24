import { useEffect, useRef, useState } from 'react';
import type { ReactNode } from 'react';
import {
  BookOpen,
  Check,
  CheckCircle2,
  Copy,
  FileSearch,
  FileText,
} from 'lucide-react';

import './ChatThread.css';

import { ToolCallCard } from './tool/ToolCallCard';

import type {
  AgentChatMessage,
  AgentProvidedContext,
  AgentSourceSearchResult,
} from '../../types/agentChat';

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

  const hasContext = Boolean(message.context);
  const hasToolCalls = Boolean(message.toolCalls?.length);
  const hasContent = Boolean(message.content.trim());
  const hasSourceSearchResults = Boolean(message.sourceSearchResults?.length);
  const attachedSourceCount = message.context?.sources.length ?? 0;

  const isAssistantStreaming =
    message.role === 'assistant' && message.isStreaming === true;

  const showInitialThinking =
    message.role === 'assistant' &&
    isAssistantStreaming &&
    !hasContext &&
    !hasSourceSearchResults &&
    !hasToolCalls;

  const showContextWaiting =
    message.role === 'assistant' &&
    isAssistantStreaming &&
    (hasContext || hasSourceSearchResults) &&
    !hasContent &&
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
      {message.context && (
        <div className="mb-4">
          <ContextProvidedCard context={message.context} />

          {showContextWaiting && !hasSourceSearchResults && (
            <div className="mt-3">
              <DotsOnlyIndicator />
            </div>
          )}
        </div>
      )}

      {hasSourceSearchResults && (
        <div className="mb-4">
          <ContextSearchedCard
            results={message.sourceSearchResults ?? []}
            attachedSourceCount={
              message.context ? attachedSourceCount : undefined
            }
          />

          {showContextWaiting && (
            <div className="mt-3">
              <DotsOnlyIndicator />
            </div>
          )}
        </div>
      )}

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

function ContextProvidedCard({ context }: { context: AgentProvidedContext }) {
  const contextSpace = context.contextSpaces[0];
  const hasMoreContextSpaces = context.contextSpaces.length > 1;

  const skillCount = context.skills.length;
  const sourceCount = context.sources.length;

  return (
    <div className="w-full min-w-0 overflow-hidden rounded-xl border border-sky-200 bg-sky-50/70 text-xs shadow-sm dark:border-sky-900/50 dark:bg-sky-950/20 dark:shadow-none">
      <div className="flex min-h-11 w-full items-center gap-2.5 px-3 py-2.5 text-left">
        <span className="inline-flex size-6 shrink-0 items-center justify-center rounded-full bg-sky-100 text-sky-700 dark:bg-sky-400/10 dark:text-sky-300">
          <BookOpen className="size-3.5" />
        </span>

        <div className="min-w-0 flex-1">
          <div className="truncate text-sm font-semibold text-zinc-950 dark:text-zinc-100">
            Context provided
          </div>

          <div className="mt-0.5 truncate text-xs text-zinc-600 dark:text-zinc-400">
            {contextSpace?.name ?? 'Runtime context'}
            {hasMoreContextSpaces
              ? ` +${context.contextSpaces.length - 1} more`
              : ''}
          </div>
        </div>

        <span className="ml-auto inline-flex shrink-0 items-center justify-center rounded-full border border-sky-200 bg-white/80 px-2.5 py-0.5 text-[11px] font-medium text-sky-700 dark:border-sky-900/60 dark:bg-sky-950/50 dark:text-sky-300">
          {formatContextSummary(skillCount, sourceCount)}
        </span>
      </div>

      <div className="grid gap-2 border-t border-sky-200/80 bg-white/60 px-3 py-3 dark:border-sky-900/50 dark:bg-zinc-950/30 sm:grid-cols-2">
        <ContextProvidedSection
          icon={<BookOpen className="size-3.5" />}
          title="Skills provided"
          emptyText="No skills provided."
          items={context.skills.map((skill) => skill.name || skill.id)}
        />

        <ContextProvidedSection
          icon={<FileText className="size-3.5" />}
          title="Sources available"
          emptyText="No sources available."
          items={context.sources.map((source) => source.name || source.id)}
        />
      </div>
    </div>
  );
}

function ContextSearchedCard({
  results,
  attachedSourceCount,
}: {
  results: AgentSourceSearchResult[];
  attachedSourceCount?: number;
}) {
  const visibleResults = results.slice(0, 4);
  const hiddenCount = Math.max(0, results.length - visibleResults.length);
  const selectedCount = results.length;
  const summary =
    attachedSourceCount === undefined
      ? formatSelectedExcerptCount(selectedCount)
      : `${formatAttachedSourceCount(
          attachedSourceCount,
        )} · ${formatSelectedExcerptCount(selectedCount)}`;

  return (
    <div className="w-full min-w-0 overflow-hidden rounded-xl border border-emerald-200 bg-emerald-50/70 text-xs shadow-sm dark:border-emerald-900/50 dark:bg-emerald-950/20 dark:shadow-none">
      <div className="flex min-h-11 w-full items-center gap-2.5 px-3 py-2.5 text-left">
        <span className="inline-flex size-6 shrink-0 items-center justify-center rounded-full bg-emerald-100 text-emerald-700 dark:bg-emerald-400/10 dark:text-emerald-300">
          <FileSearch className="size-3.5" />
        </span>

        <div className="min-w-0 flex-1">
          <div className="truncate text-sm font-semibold text-zinc-950 dark:text-zinc-100">
            Context searched
          </div>

          <div className="mt-0.5 truncate text-xs text-zinc-600 dark:text-zinc-400">
            {summary}
          </div>
        </div>

        <span className="ml-auto inline-flex shrink-0 items-center justify-center rounded-full border border-emerald-200 bg-white/80 px-2.5 py-0.5 text-[11px] font-medium text-emerald-700 dark:border-emerald-900/60 dark:bg-emerald-950/50 dark:text-emerald-300">
          Selected
        </span>
      </div>

      <div className="flex flex-col gap-2 border-t border-emerald-200/80 bg-white/60 px-3 py-3 dark:border-emerald-900/50 dark:bg-zinc-950/30">
        {visibleResults.map((result) => (
          <div
            key={`${result.sourceId}-${result.relativePath}-${result.snippet}`}
            className="relative min-w-0 rounded-lg border border-zinc-200 bg-white px-3 py-2.5 pr-10 dark:border-zinc-800 dark:bg-zinc-950/70"
          >
            <span
              className="absolute right-3 top-3 inline-flex text-emerald-600 dark:text-emerald-300"
              aria-label="Sent to model context"
              title="Sent to model context"
            >
              <CheckCircle2 className="size-4" aria-hidden="true" />
            </span>

            <div className="mb-1.5 flex min-w-0 flex-wrap items-center gap-1.5 text-xs font-semibold text-zinc-700 dark:text-zinc-300">
              <FileText className="size-3.5 shrink-0 text-zinc-400 dark:text-zinc-500" />

              <span className="min-w-0 max-w-full truncate">
                {result.sourceName || result.sourceId}
              </span>

              <span className="min-w-0 max-w-full truncate text-zinc-500 dark:text-zinc-400">
                {result.relativePath || result.fileName}
              </span>

              <span className="shrink-0 rounded-full border border-zinc-200 bg-zinc-50 px-2 py-0.5 text-[11px] font-medium text-zinc-600 dark:border-zinc-800 dark:bg-zinc-900/70 dark:text-zinc-400">
                Score {result.score.toFixed(2)}
              </span>
            </div>

            <div className="max-h-16 overflow-hidden whitespace-pre-wrap break-words text-xs leading-5 text-zinc-600 dark:text-zinc-400">
              {result.snippet}
            </div>
          </div>
        ))}

        {hiddenCount > 0 && (
          <div className="text-xs text-zinc-500 dark:text-zinc-400">
            +{hiddenCount} more selected excerpt
            {hiddenCount === 1 ? '' : 's'}
          </div>
        )}
      </div>
    </div>
  );
}

function formatAttachedSourceCount(value: number): string {
  return `${value} attached source${value === 1 ? '' : 's'}`;
}

function formatSelectedExcerptCount(value: number): string {
  return `${value} selected excerpt${value === 1 ? '' : 's'}`;
}

function ContextProvidedSection({
  icon,
  title,
  emptyText,
  items,
}: {
  icon: ReactNode;
  title: string;
  emptyText: string;
  items: string[];
}) {
  return (
    <div className="min-w-0 rounded-lg border border-zinc-200 bg-white px-3 py-2.5 dark:border-zinc-800 dark:bg-zinc-950/70">
      <div className="mb-2 flex items-center gap-1.5 text-xs font-semibold text-zinc-700 dark:text-zinc-300">
        <span className="text-zinc-400 dark:text-zinc-500">
          {icon}
        </span>
        {title}
      </div>

      {items.length === 0 ? (
        <div className="text-xs text-zinc-500 dark:text-zinc-500">
          {emptyText}
        </div>
      ) : (
        <div className="flex min-w-0 flex-wrap gap-1.5">
          {items.slice(0, 4).map((item) => (
            <span
              key={item}
              className="min-w-0 max-w-full truncate rounded-full border border-zinc-200 bg-zinc-50 px-2 py-0.5 text-[11px] font-medium text-zinc-700 dark:border-zinc-800 dark:bg-zinc-900/70 dark:text-zinc-300"
              title={item}
            >
              {item}
            </span>
          ))}

          {items.length > 4 && (
            <span className="rounded-full border border-zinc-200 bg-zinc-50 px-2 py-0.5 text-[11px] font-medium text-zinc-500 dark:border-zinc-800 dark:bg-zinc-900/70 dark:text-zinc-400">
              +{items.length - 4} more
            </span>
          )}
        </div>
      )}
    </div>
  );
}

function formatContextSummary(skillCount: number, sourceCount: number): string {
  const parts: string[] = [];

  if (skillCount > 0) {
    parts.push(`${skillCount} skill${skillCount === 1 ? '' : 's'}`);
  }

  if (sourceCount > 0) {
    parts.push(`${sourceCount} source${sourceCount === 1 ? '' : 's'}`);
  }

  return parts.length > 0 ? parts.join(' · ') : 'Context';
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

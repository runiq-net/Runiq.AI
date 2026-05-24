import { useState } from 'react';
import { LoaderCircle, Wrench } from 'lucide-react';

import type { AgentToolCall } from '../../../types/agentChat';
import { ToolJsonBlock } from './ToolJsonBlock';

type ToolCallCardProps = {
  toolCall: AgentToolCall;
  forceWorking?: boolean;
};

export function ToolCallCard({
  toolCall,
  forceWorking = false,
}: ToolCallCardProps) {
  const [isOpen, setIsOpen] = useState(false);

  const displayStatus =
    forceWorking && toolCall.status !== 'failed'
      ? 'running'
      : toolCall.status;

  const isRunning = displayStatus === 'running';
  const isCompleted = displayStatus === 'completed';
  const isFailed = displayStatus === 'failed';

  return (
    <div className="w-full min-w-0 overflow-hidden rounded-xl border border-zinc-200 bg-white text-xs shadow-sm dark:border-zinc-800 dark:bg-zinc-950/60 dark:shadow-none">
      <button
        type="button"
        onClick={() => setIsOpen((current) => !current)}
        className="flex min-h-11 w-full items-center gap-2.5 px-3 py-2 text-left transition hover:bg-zinc-50 dark:hover:bg-zinc-900/70"
      >
        <span className="w-3 text-sm leading-none text-zinc-400 dark:text-zinc-500">
          {isOpen ? '⌄' : '›'}
        </span>

        <span
          className={[
            'inline-flex size-6 shrink-0 items-center justify-center rounded-full bg-amber-100 text-amber-600 dark:bg-amber-400/10 dark:text-amber-300',
            isRunning ? 'animate-pulse' : '',
          ].join(' ')}
        >
          {isRunning ? (
            <LoaderCircle className="size-3.5 animate-spin" />
          ) : (
            <Wrench className="size-3.5" />
          )}
        </span>

        <span className="min-w-0 flex-1 truncate font-mono text-sm font-semibold text-zinc-950 dark:text-zinc-100">
          {formatToolDisplayName(toolCall.name)}
        </span>

        <span
          className={[
            'ml-auto inline-flex min-w-24 shrink-0 items-center justify-center rounded-full border px-2.5 py-0.5 text-[11px] font-medium',
            isCompleted
              ? 'border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-900/60 dark:bg-emerald-950/30 dark:text-emerald-300'
              : '',
            isFailed
              ? 'border-red-200 bg-red-50 text-red-700 dark:border-red-900/60 dark:bg-red-950/30 dark:text-red-300'
              : '',
            isRunning
              ? 'border-amber-200 bg-amber-50 text-amber-700 dark:border-amber-900/60 dark:bg-amber-950/30 dark:text-amber-300'
              : '',
          ].join(' ')}
        >
          {isRunning && (
            <span className="mr-1.5 inline-block size-1.5 animate-pulse rounded-full bg-current" />
          )}
          {formatToolStatus(displayStatus)}
        </span>
      </button>

      {isOpen && (
        <div className="space-y-4 border-t border-zinc-200 bg-zinc-50/70 px-3 py-3 dark:border-zinc-800 dark:bg-zinc-950">
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
              <div className="mb-2 text-sm font-semibold text-zinc-950 dark:text-zinc-100">
                Tool error
              </div>

              <pre className="max-h-72 overflow-auto whitespace-pre-wrap break-words rounded-xl border border-red-200 bg-red-50 p-3 text-xs leading-6 text-red-700 dark:border-red-900/60 dark:bg-red-950/20 dark:text-red-300">
                {toolCall.errorMessage ?? 'Tool execution failed.'}
              </pre>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

function formatToolDisplayName(value: string): string {
  return value
    .replace(/[-_]+/g, ' ')
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    .trim()
    .split(/\s+/)
    .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
    .join(' ');
}

function formatToolStatus(status: AgentToolCall['status']): string {
  if (status === 'running') {
    return 'Working...';
  }

  if (status === 'completed') {
    return 'Completed';
  }

  return 'Failed';
}

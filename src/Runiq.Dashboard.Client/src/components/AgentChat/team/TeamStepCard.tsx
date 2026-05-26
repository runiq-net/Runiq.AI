import { useState } from 'react';
import { CheckCircle2, LoaderCircle,  XCircle } from 'lucide-react';

import type { AgentTeamStep } from '../../../types/agentChat';
import { ToolCallCard } from '../tool/ToolCallCard';

type TeamStepCardProps = {
  step: AgentTeamStep;
};

export function TeamStepCard({ step }: TeamStepCardProps) {
  const [isOpen, setIsOpen] = useState(false);

  const isRunning = step.status === 'running';
  const isCompleted = step.status === 'completed';
  const isFailed = step.status === 'failed';

  const hasToolCalls = Boolean(step.toolCalls?.length);
  const hasDetails = Boolean(
    hasToolCalls || step.content?.trim() || step.errorMessage?.trim(),
  );

  return (
    <div className="w-full min-w-0 overflow-hidden rounded-xl border border-zinc-200 bg-white text-xs shadow-sm dark:border-zinc-800 dark:bg-zinc-950/60 dark:shadow-none">
      <button
        type="button"
        onClick={() => setIsOpen((current) => !current)}
        className="flex min-h-11 w-full items-center gap-2.5 px-3 py-2 text-left transition hover:bg-zinc-50 dark:hover:bg-zinc-900/70"
      >
        <span className="w-3 text-sm leading-none text-zinc-400 dark:text-zinc-500">
          {hasDetails ? (isOpen ? '⌄' : '›') : ''}
        </span>

        <span
          className={[
            'inline-flex size-6 shrink-0 items-center justify-center rounded-full',
            isRunning
              ? 'bg-amber-100 text-amber-600 dark:bg-amber-400/10 dark:text-amber-300'
              : '',
            isCompleted
              ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-400/10 dark:text-emerald-300'
              : '',
            isFailed
              ? 'bg-red-100 text-red-700 dark:bg-red-400/10 dark:text-red-300'
              : '',
          ].join(' ')}
        >
          {isRunning && <LoaderCircle className="size-3.5 animate-spin" />}
          {isCompleted && <CheckCircle2 className="size-3.5" />}
          {isFailed && <XCircle className="size-3.5" />}
        </span>

        <div className="min-w-0 flex-1">
          <div className="truncate text-sm font-semibold text-zinc-950 dark:text-zinc-100">
            {step.role}
          </div>

          <div className="mt-0.5 truncate font-mono text-[11px] text-zinc-500 dark:text-zinc-500">
            {step.agentId}
          </div>
        </div>

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
          {formatTeamStepStatus(step.status)}
        </span>
      </button>

      {isOpen && hasDetails && (
        <div className="space-y-3 border-t border-zinc-200 bg-zinc-50/70 px-3 py-3 dark:border-zinc-800 dark:bg-zinc-950">
          {hasToolCalls && (
            <div>
              <div className="mb-2 text-sm font-semibold text-zinc-950 dark:text-zinc-100">
                Tool calls
              </div>

              <div className="space-y-2">
                {step.toolCalls?.map((toolCall) => (
                  <ToolCallCard key={toolCall.id} toolCall={toolCall} />
                ))}
              </div>
            </div>
          )}

          {step.content && (
            <div>
              <div className="mb-2 text-sm font-semibold text-zinc-950 dark:text-zinc-100">
                Agent contribution
              </div>

              <pre className="max-h-72 overflow-auto whitespace-pre-wrap break-words rounded-xl border border-zinc-200 bg-white p-3 text-xs leading-6 text-zinc-700 dark:border-zinc-800 dark:bg-zinc-950/70 dark:text-zinc-300">
                {step.content}
              </pre>
            </div>
          )}

          {step.errorMessage && (
            <div>
              <div className="mb-2 text-sm font-semibold text-zinc-950 dark:text-zinc-100">
                Member error
              </div>

              <pre className="max-h-72 overflow-auto whitespace-pre-wrap break-words rounded-xl border border-red-200 bg-red-50 p-3 text-xs leading-6 text-red-700 dark:border-red-900/60 dark:bg-red-950/20 dark:text-red-300">
                {step.errorCode ? `${step.errorCode}: ` : ''}
                {step.errorMessage}
              </pre>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

function formatTeamStepStatus(status: AgentTeamStep['status']): string {
  if (status === 'running') {
    return 'Working...';
  }

  if (status === 'completed') {
    return 'Completed';
  }

  return 'Failed';
}

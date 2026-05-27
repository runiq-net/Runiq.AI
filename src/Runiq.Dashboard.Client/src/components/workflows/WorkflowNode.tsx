import { Bot, CheckCircle2, Circle, CircleAlert, Loader2 } from 'lucide-react';
import { Handle, Position, type NodeProps } from '@xyflow/react';

import type { WorkflowNodeData, WorkflowStepStatus } from './workflowTypes';

const statusStyles: Record<WorkflowStepStatus, string> = {
  NotStarted:
    'border-zinc-200 bg-zinc-100 text-zinc-600 dark:border-zinc-800 dark:bg-zinc-900 dark:text-zinc-400',
  Running:
    'border-blue-200 bg-blue-50 text-blue-700 dark:border-blue-900/70 dark:bg-blue-950/30 dark:text-blue-300',
  Completed:
    'border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-900/70 dark:bg-emerald-950/30 dark:text-emerald-300',
  Failed:
    'border-red-200 bg-red-50 text-red-700 dark:border-red-900/70 dark:bg-red-950/30 dark:text-red-300',
  Skipped:
    'border-zinc-200 bg-zinc-50 text-zinc-400 dark:border-zinc-800 dark:bg-zinc-950 dark:text-zinc-600',
};

const nodeStatusStyles: Record<WorkflowStepStatus, string> = {
  NotStarted: 'border-zinc-200 dark:border-zinc-800',
  Running:
    'border-blue-300 shadow-blue-500/10 ring-1 ring-blue-500/15 dark:border-blue-800 dark:shadow-blue-500/10 dark:ring-blue-400/20',
  Completed:
    'border-emerald-300 shadow-emerald-500/10 dark:border-emerald-800 dark:shadow-emerald-500/10',
  Failed:
    'border-red-300 shadow-red-500/10 ring-1 ring-red-500/10 dark:border-red-800 dark:shadow-red-500/10 dark:ring-red-400/15',
  Skipped: 'border-zinc-200 opacity-70 dark:border-zinc-800',
};

export function WorkflowNode({ data }: NodeProps) {
  const nodeData = data as WorkflowNodeData;
  const { step, status, isSelected, toolCalls } = nodeData;
  const toolSummary = formatToolSummary(toolCalls);

  return (
    <div
      className={[
        'w-56 rounded-xl border bg-white p-4 shadow-sm transition dark:bg-zinc-950',
        nodeStatusStyles[status],
        status === 'Running' ? 'animate-pulse' : '',
        isSelected
          ? 'ring-2 ring-zinc-900/20 dark:ring-zinc-100/25'
          : '',
      ].join(' ')}
    >
      <Handle
        type="target"
        position={Position.Left}
        isConnectable={false}
        className="!size-2 !border-zinc-300 !bg-zinc-200 dark:!border-zinc-700 dark:!bg-zinc-800"
      />

      <div className="flex items-start justify-between gap-3">
        <span className="inline-flex size-8 shrink-0 items-center justify-center rounded-lg border border-zinc-200 bg-zinc-50 text-zinc-600 dark:border-zinc-800 dark:bg-zinc-900 dark:text-zinc-300">
          <Bot className="size-4" />
        </span>

        <StatusBadge status={status} />
      </div>

      <div className="mt-4 min-w-0">
        <div className="truncate text-sm font-semibold text-zinc-950 dark:text-zinc-100">
          {toDisplayName(step.id)}
        </div>

        <div className="mt-1 truncate font-mono text-xs text-zinc-500 dark:text-zinc-500">
          {step.agentName}
        </div>
      </div>

      <div className="mt-4 rounded-lg border border-zinc-200 bg-zinc-50 px-2.5 py-2 text-[11px] font-medium text-zinc-600 dark:border-zinc-800 dark:bg-zinc-900/80 dark:text-zinc-400">
        {formatFailurePolicy(step.failureBehavior, step.failureStepId)}
      </div>

      {toolSummary ? (
        <div
          className={[
            'mt-2 inline-flex max-w-full rounded-full border px-2 py-1 text-[11px] font-semibold',
            toolSummary.hasFailed
              ? 'border-red-200 bg-red-50 text-red-700 dark:border-red-900/70 dark:bg-red-950/30 dark:text-red-300'
              : 'border-zinc-200 bg-zinc-50 text-zinc-600 dark:border-zinc-800 dark:bg-zinc-900/70 dark:text-zinc-400',
          ].join(' ')}
        >
          {toolSummary.label}
        </div>
      ) : null}

      <Handle
        type="source"
        position={Position.Right}
        isConnectable={false}
        className="!size-2 !border-zinc-300 !bg-zinc-200 dark:!border-zinc-700 dark:!bg-zinc-800"
      />
      <Handle
        id="failure"
        type="source"
        position={Position.Bottom}
        isConnectable={false}
        className="!size-2 !border-amber-300 !bg-amber-300 dark:!border-amber-700 dark:!bg-amber-600"
      />
    </div>
  );
}

function formatToolSummary(
  toolCalls: WorkflowNodeData['toolCalls'],
): { label: string; hasFailed: boolean } | null {
  if (toolCalls.length === 0) {
    return null;
  }

  const failedCount = toolCalls.filter((toolCall) =>
    equalsStatus(toolCall.status, 'Failed'),
  ).length;

  if (failedCount > 0) {
    return {
      label: `${failedCount} failed tool${failedCount === 1 ? '' : 's'}`,
      hasFailed: true,
    };
  }

  return {
    label: `${toolCalls.length} tool${toolCalls.length === 1 ? '' : 's'}`,
    hasFailed: false,
  };
}

function equalsStatus(status: string, expected: string): boolean {
  return status.toLowerCase() === expected.toLowerCase();
}

function StatusBadge({ status }: { status: WorkflowStepStatus }) {
  const Icon =
    status === 'Completed'
      ? CheckCircle2
      : status === 'Failed'
        ? CircleAlert
        : status === 'Running'
          ? Loader2
          : Circle;

  return (
    <span
      className={[
        'inline-flex shrink-0 items-center gap-1.5 rounded-full border px-2 py-1 text-[11px] font-semibold',
        statusStyles[status],
      ].join(' ')}
    >
      <Icon
        className={[
          'size-3',
          status === 'Running' ? 'animate-spin' : '',
        ].join(' ')}
      />
      {status}
    </span>
  );
}

function formatFailurePolicy(
  failureBehavior: string,
  failureStepId?: string | null,
): string {
  if (failureBehavior === 'Stop') {
    return 'On failure: stop';
  }

  if (failureStepId) {
    return `On failure: ${failureBehavior.toLowerCase()} -> ${failureStepId}`;
  }

  return `On failure: ${failureBehavior.toLowerCase()}`;
}

function toDisplayName(value: string): string {
  return value
    .replace(/[-_]+/g, ' ')
    .replace(/\b\w/g, (part) => part.toUpperCase());
}

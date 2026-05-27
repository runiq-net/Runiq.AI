import { Check, Play } from 'lucide-react';
import { Handle, Position, type NodeProps } from '@xyflow/react';

import type { WorkflowTerminalNodeData } from './workflowTypes';

export function WorkflowTerminalNode({ data }: NodeProps) {
  const nodeData = data as WorkflowTerminalNodeData;
  const Icon = nodeData.kind === 'start' ? Play : Check;

  return (
    <div
      className={[
        'flex h-16 min-w-28 items-center gap-2 rounded-full border bg-white px-4 shadow-sm transition dark:bg-zinc-950',
        nodeData.isSelected
          ? 'border-zinc-900 ring-2 ring-zinc-900/10 dark:border-zinc-100 dark:ring-zinc-100/10'
          : 'border-zinc-200 dark:border-zinc-800',
      ].join(' ')}
    >
      {nodeData.kind === 'end' ? (
        <Handle
          type="target"
          position={Position.Left}
          isConnectable={false}
          className="!size-2 !border-zinc-300 !bg-zinc-200 dark:!border-zinc-700 dark:!bg-zinc-800"
        />
      ) : null}

      <span
        className={[
          'inline-flex size-8 shrink-0 items-center justify-center rounded-full border',
          nodeData.kind === 'start'
            ? 'border-blue-200 bg-blue-50 text-blue-700 dark:border-blue-900/70 dark:bg-blue-950/30 dark:text-blue-300'
            : 'border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-900/70 dark:bg-emerald-950/30 dark:text-emerald-300',
        ].join(' ')}
      >
        <Icon className="size-4" />
      </span>

      <span className="text-sm font-semibold text-zinc-900 dark:text-zinc-100">
        {nodeData.label}
      </span>

      {nodeData.kind === 'start' ? (
        <Handle
          type="source"
          position={Position.Right}
          isConnectable={false}
          className="!size-2 !border-zinc-300 !bg-zinc-200 dark:!border-zinc-700 dark:!bg-zinc-800"
        />
      ) : null}
    </div>
  );
}

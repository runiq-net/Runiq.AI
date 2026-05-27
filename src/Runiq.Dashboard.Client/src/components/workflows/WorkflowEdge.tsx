import {
  BaseEdge,
  EdgeLabelRenderer,
  getSmoothStepPath,
  type EdgeProps,
} from '@xyflow/react';

import type { WorkflowEdgeData } from './workflowTypes';

export function WorkflowEdge({
  id,
  sourceX,
  sourceY,
  targetX,
  targetY,
  sourcePosition,
  targetPosition,
  markerEnd,
  data,
}: EdgeProps) {
  const edgeData = data as WorkflowEdgeData | undefined;
  const [edgePath, labelX, labelY] = getSmoothStepPath({
    sourceX,
    sourceY,
    sourcePosition,
    targetX,
    targetY,
    targetPosition,
    borderRadius: edgeData?.variant === 'success' ? 18 : 28,
    offset: edgeData?.variant === 'success' ? 24 : 44,
  });
  const isFailure =
    edgeData?.variant === 'failure-continue' ||
    edgeData?.variant === 'failure-goto';

  return (
    <>
      <BaseEdge
        id={id}
        path={edgePath}
        className={
          isFailure
            ? 'stroke-amber-500 dark:stroke-amber-400'
            : edgeData?.variant === 'start'
              ? 'stroke-blue-500 dark:stroke-blue-400'
              : 'stroke-emerald-500 dark:stroke-emerald-400'
        }
        markerEnd={markerEnd}
        style={{
          strokeWidth: 2,
        }}
      />

      {edgeData?.label ? (
        <EdgeLabelRenderer>
          <div
            className={[
              'nodrag nopan absolute -translate-x-1/2 -translate-y-1/2 rounded-full border px-2 py-0.5 text-[10px] font-semibold shadow-sm',
              isFailure
                ? 'border-amber-200 bg-amber-50 text-amber-700 dark:border-amber-900/70 dark:bg-amber-950 dark:text-amber-300'
                : edgeData?.variant === 'start'
                  ? 'border-blue-200 bg-blue-50 text-blue-700 dark:border-blue-900/70 dark:bg-blue-950 dark:text-blue-300'
                  : 'border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-900/70 dark:bg-emerald-950 dark:text-emerald-300',
            ].join(' ')}
            style={{
              transform: `translate(-50%, -50%) translate(${labelX}px,${labelY}px)`,
            }}
          >
            {edgeData.label}
          </div>
        </EdgeLabelRenderer>
      ) : null}
    </>
  );
}

import { MarkerType, type Edge, type Node } from '@xyflow/react';

import type { WorkflowMetadata } from '../../api/agentMetadataApi';
import {
  getStepRunResult,
  resolveStepStatus,
  workflowEndNodeId,
  workflowStartNodeId,
  type WorkflowEdgeData,
  type WorkflowGraphInput,
  type WorkflowNodeData,
  type WorkflowTerminalNodeData,
} from './workflowTypes';

const horizontalGap = 340;
const baseY = 130;

export function mapWorkflowToGraph({
  workflow,
  runResult,
  selectedStepId,
  isRunning,
}: WorkflowGraphInput): {
  nodes: Array<Node<WorkflowNodeData | WorkflowTerminalNodeData>>;
  edges: Edge<WorkflowEdgeData>[];
} {
  const nodes: Array<Node<WorkflowNodeData | WorkflowTerminalNodeData>> = [
    {
      id: workflowStartNodeId,
      type: 'runiqTerminalNode',
      position: { x: 0, y: baseY + 32 },
      data: {
        kind: 'start',
        label: 'Start',
        status: isRunning ? 'Running' : 'NotStarted',
        isSelected: selectedStepId === workflowStartNodeId,
      },
      connectable: false,
      selectable: true,
    },
    ...workflow.steps.map<Node<WorkflowNodeData>>((step, index) => ({
      id: step.id,
      type: 'runiqWorkflowNode',
      position: {
        x: (index + 1) * horizontalGap,
        y: baseY,
      },
      data: {
        kind: 'step',
        step,
        status: resolveStepStatus(
          step,
          runResult,
          isRunning,
          workflow.startStepId ?? workflow.steps[0]?.id,
        ),
        isSelected: selectedStepId?.toLowerCase() === step.id.toLowerCase(),
        toolCalls: getStepRunResult(runResult, step.id)?.toolCalls ?? [],
      },
      connectable: false,
      selectable: true,
    })),
    {
      id: workflowEndNodeId,
      type: 'runiqTerminalNode',
      position: {
        x: (workflow.steps.length + 1) * horizontalGap,
        y: baseY + 32,
      },
      data: {
        kind: 'end',
        label: 'End',
        status:
          runResult?.status === 'Completed'
            ? 'Completed'
            : runResult?.status === 'Failed'
              ? 'Failed'
              : 'NotStarted',
        isSelected: selectedStepId === workflowEndNodeId,
      },
      connectable: false,
      selectable: true,
    },
  ];

  return {
    nodes,
    edges: mapWorkflowEdges(workflow),
  };
}

function mapWorkflowEdges(
  workflow: WorkflowMetadata,
): Edge<WorkflowEdgeData>[] {
  const edges: Edge<WorkflowEdgeData>[] = [];
  const firstStepId = workflow.startStepId ?? workflow.steps[0]?.id;

  if (firstStepId) {
    edges.push(
      createEdge(
        `${workflowStartNodeId}-start-${firstStepId}`,
        workflowStartNodeId,
        firstStepId,
        '',
        'start',
      ),
    );
  }

  workflow.steps.forEach((step) => {
    if (step.successStepId) {
      edges.push(
        createEdge(
          `${step.id}-success-${step.successStepId}`,
          step.id,
          step.successStepId,
          '',
          'success',
        ),
      );
    } else {
      edges.push(
        createEdge(`${step.id}-end`, step.id, workflowEndNodeId, '', 'end'),
      );
    }

    if (step.failureStepId) {
      const behavior = step.failureBehavior.toLowerCase();
      const variant =
        behavior === 'goto' ? 'failure-goto' : 'failure-continue';

      edges.push({
        ...createEdge(
          `${step.id}-failure-${step.failureStepId}`,
          step.id,
          step.failureStepId,
          behavior === 'goto' ? 'failure / goto' : 'failure / continue',
          variant,
        ),
        sourceHandle: 'failure',
      });
    }
  });

  return edges;
}

function createEdge(
  id: string,
  source: string,
  target: string,
  label: string,
  variant: WorkflowEdgeData['variant'],
): Edge<WorkflowEdgeData> {
  return {
    id,
    source,
    target,
    type: 'runiqWorkflowEdge',
    markerEnd: {
      type: MarkerType.ArrowClosed,
      width: 16,
      height: 16,
    },
    data: {
      label,
      variant,
    },
  };
}

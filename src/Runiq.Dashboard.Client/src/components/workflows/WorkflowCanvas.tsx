import { useMemo } from 'react';
import {
  Background,
  Controls,
  ReactFlow,
  type EdgeTypes,
  type NodeTypes,
} from '@xyflow/react';

import type {
  WorkflowMetadata,
  WorkflowRunResponse,
} from '../../api/agentMetadataApi';
import { WorkflowEdge } from './WorkflowEdge';
import { WorkflowNode } from './WorkflowNode';
import { WorkflowTerminalNode } from './WorkflowTerminalNode';
import { mapWorkflowToGraph } from './workflowGraphMapper';
import './WorkflowCanvas.css';

const nodeTypes: NodeTypes = {
  runiqWorkflowNode: WorkflowNode,
  runiqTerminalNode: WorkflowTerminalNode,
};

const edgeTypes: EdgeTypes = {
  runiqWorkflowEdge: WorkflowEdge,
};

type WorkflowCanvasProps = {
  workflow: WorkflowMetadata;
  runResult: WorkflowRunResponse | null;
  selectedStepId: string | null;
  isRunning: boolean;
  onSelectStep: (stepId: string) => void;
};

export function WorkflowCanvas({
  workflow,
  runResult,
  selectedStepId,
  isRunning,
  onSelectStep,
}: WorkflowCanvasProps) {
  const { nodes, edges } = useMemo(
    () =>
      mapWorkflowToGraph({
        workflow,
        runResult,
        selectedStepId,
        isRunning,
      }),
    [isRunning, runResult, selectedStepId, workflow],
  );

  return (
    <div className="runiq-workflow-canvas h-full min-h-0 overflow-hidden bg-zinc-50 dark:bg-zinc-950">
      <ReactFlow
        nodes={nodes}
        edges={edges}
        nodeTypes={nodeTypes}
        edgeTypes={edgeTypes}
        fitView
        fitViewOptions={{ padding: 0.24 }}
        nodesDraggable
        nodesConnectable={false}
        elementsSelectable
        deleteKeyCode={null}
        onNodeClick={(_, node) => onSelectStep(node.id)}
        proOptions={{ hideAttribution: true }}
      >
        <Background
          gap={24}
          size={1}
          className="fill-zinc-200 dark:fill-zinc-800"
        />
        <Controls showInteractive={false} />
      </ReactFlow>
    </div>
  );
}

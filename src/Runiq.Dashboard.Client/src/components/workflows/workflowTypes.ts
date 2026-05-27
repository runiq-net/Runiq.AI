import type {
  WorkflowMetadata,
  WorkflowRunResponse,
  WorkflowStepMetadata,
  WorkflowStepRunResult,
  WorkflowToolCallRunResult,
} from '../../api/agentMetadataApi';

export type WorkflowStepStatus =
  | 'NotStarted'
  | 'Running'
  | 'Completed'
  | 'Failed'
  | 'Skipped';

export type WorkflowNodeData = {
  kind: 'step';
  step: WorkflowStepMetadata;
  status: WorkflowStepStatus;
  isSelected: boolean;
  toolCalls: WorkflowToolCallRunResult[];
};

export type WorkflowTerminalNodeKind = 'start' | 'end';

export type WorkflowTerminalNodeData = {
  kind: WorkflowTerminalNodeKind;
  label: string;
  status: WorkflowStepStatus;
  isSelected: boolean;
};

export type WorkflowEdgeData = {
  label: string;
  variant: 'start' | 'success' | 'failure-continue' | 'failure-goto' | 'end';
};

export const workflowStartNodeId = '__runiq_start__';

export const workflowEndNodeId = '__runiq_end__';

export type WorkflowGraphInput = {
  workflow: WorkflowMetadata;
  runResult?: WorkflowRunResponse | null;
  selectedStepId?: string | null;
  isRunning?: boolean;
};

export function getStepRunResult(
  runResult: WorkflowRunResponse | null | undefined,
  stepId: string,
): WorkflowStepRunResult | undefined {
  return runResult?.steps.find(
    (step) => step.stepId.toLowerCase() === stepId.toLowerCase(),
  );
}

export function resolveStepStatus(
  step: WorkflowStepMetadata,
  runResult: WorkflowRunResponse | null | undefined,
  isRunning: boolean | undefined,
  startStepId?: string | null,
): WorkflowStepStatus {
  const stepResult = getStepRunResult(runResult, step.id);

  if (stepResult?.status === 'Completed') {
    return 'Completed';
  }

  if (stepResult?.status === 'Failed') {
    return 'Failed';
  }

  if (
    isRunning &&
    !runResult &&
    (startStepId?.toLowerCase() ?? '') === step.id.toLowerCase()
  ) {
    return 'Running';
  }

  return 'NotStarted';
}

export function pickDefaultSelectedStepId(
  workflow: WorkflowMetadata,
  runResult: WorkflowRunResponse | null,
): string | null {
  const failedStep = runResult?.steps.find((step) => step.status === 'Failed');

  if (failedStep) {
    return failedStep.stepId;
  }

  const completedSteps =
    runResult?.steps.filter((step) => step.status === 'Completed') ?? [];

  if (completedSteps.length > 0) {
    return completedSteps.at(-1)?.stepId ?? null;
  }

  return workflow.startStepId ?? workflow.steps[0]?.id ?? null;
}

export function pickDefaultSelectedNodeId(
  _workflow: WorkflowMetadata,
  runResult: WorkflowRunResponse | null,
): string {
  const failedStep = runResult?.steps.find((step) => step.status === 'Failed');

  if (failedStep) {
    return failedStep.stepId;
  }

  const completedSteps =
    runResult?.steps.filter((step) => step.status === 'Completed') ?? [];

  if (completedSteps.length > 0) {
    return completedSteps.at(-1)?.stepId ?? workflowEndNodeId;
  }

  if (runResult) {
    return workflowEndNodeId;
  }

  return workflowStartNodeId;
}

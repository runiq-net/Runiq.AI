import { useCallback, useEffect, useState } from 'react';

import {
  getWorkflow,
  runWorkflow,
  type WorkflowMetadata,
  type WorkflowRunResponse,
} from '../api/agentMetadataApi';
import { WorkflowCanvas } from '../components/workflows/WorkflowCanvas';
import { WorkflowInspectorPanel } from '../components/workflows/WorkflowInspectorPanel';
import { WorkflowRunBar } from '../components/workflows/WorkflowRunBar';
import { pickDefaultSelectedNodeId } from '../components/workflows/workflowTypes';
import { getDashboardBasePath } from '../dashboardConfig';
import { WorkflowStudioLayout } from '../layouts/WorkflowStudioLayout';

type WorkflowDetailPageProps = {
  workflowId: string;
};

export function WorkflowDetailPage({ workflowId }: WorkflowDetailPageProps) {
  const [workflow, setWorkflow] = useState<WorkflowMetadata | null>(null);
  const [runResult, setRunResult] = useState<WorkflowRunResponse | null>(null);
  const [selectedNodeId, setSelectedNodeId] = useState<string | null>(null);
  const [input, setInput] = useState('');
  const [isLoading, setLoading] = useState(true);
  const [isRunning, setRunning] = useState(false);
  const [isInspectorCollapsed, setInspectorCollapsed] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [runErrorMessage, setRunErrorMessage] = useState<string | null>(null);

  useEffect(() => {
    let isMounted = true;

    async function loadWorkflow() {
      try {
        setLoading(true);
        setErrorMessage(null);
        setRunResult(null);

        const result = await getWorkflow(getDashboardBasePath(), workflowId);

        if (isMounted) {
          setWorkflow(result);
          setSelectedNodeId(pickDefaultSelectedNodeId(result, null));
        }
      } catch (error) {
        if (isMounted) {
          setErrorMessage(
            error instanceof Error
              ? error.message
              : 'Workflow metadata could not be loaded.',
          );
        }
      } finally {
        if (isMounted) {
          setLoading(false);
        }
      }
    }

    void loadWorkflow();

    return () => {
      isMounted = false;
    };
  }, [workflowId]);

  const handleRun = useCallback(async () => {
    if (!workflow || input.trim().length === 0) {
      return;
    }

    try {
      setRunning(true);
      setRunErrorMessage(null);

      const result = await runWorkflow(
        getDashboardBasePath(),
        workflow.id,
        input.trim(),
      );

      setRunResult(result);
      setSelectedNodeId(pickDefaultSelectedNodeId(workflow, result));
    } catch (error) {
      setRunErrorMessage(
        error instanceof Error
          ? error.message
          : 'Workflow run could not be completed.',
      );
    } finally {
      setRunning(false);
    }
  }, [input, workflow]);

  const status = isRunning ? 'Running' : runResult?.status ?? 'Not run';

  if (isLoading) {
    return (
      <WorkflowStudioLayout workflowName="Loading workflow" status="Loading">
        <WorkflowDetailLoadingState />
      </WorkflowStudioLayout>
    );
  }

  if (errorMessage || !workflow) {
    return (
      <WorkflowStudioLayout workflowName="Workflow" status="Failed">
        <WorkflowDetailErrorState
          message={errorMessage ?? 'Workflow metadata could not be loaded.'}
        />
      </WorkflowStudioLayout>
    );
  }

  return (
    <WorkflowStudioLayout
      workflowName={workflow.name}
      status={status}
    >
      <div className="flex h-full min-h-0 flex-col">
        <WorkflowRunBar
          input={input}
          isRunning={isRunning}
          errorMessage={runErrorMessage}
          onInputChange={setInput}
          onRun={handleRun}
        />

        <div className="flex min-h-0 flex-1 flex-col lg:flex-row">
          <main className="min-h-[420px] min-w-0 flex-1">
            <WorkflowCanvas
              workflow={workflow}
              runResult={runResult}
              selectedStepId={selectedNodeId}
              isRunning={isRunning}
              onSelectStep={(nodeId) => {
                setSelectedNodeId(nodeId);
                setInspectorCollapsed(false);
              }}
            />
          </main>

          <WorkflowInspectorPanel
            workflow={workflow}
            runResult={runResult}
            selectedNodeId={selectedNodeId}
            currentInput={input}
            isCollapsed={isInspectorCollapsed}
            onCollapseChange={setInspectorCollapsed}
          />
        </div>
      </div>
    </WorkflowStudioLayout>
  );
}

function WorkflowDetailLoadingState() {
  return (
    <div className="flex h-full min-h-0 flex-col">
      <div className="h-[65px] shrink-0 animate-pulse border-b border-zinc-200 bg-zinc-100 dark:border-zinc-800 dark:bg-zinc-900/80" />
      <div className="min-h-0 flex-1 animate-pulse bg-zinc-100 dark:bg-zinc-900/70" />
    </div>
  );
}

function WorkflowDetailErrorState({ message }: { message: string }) {
  return (
    <div className="m-4 rounded-xl border border-red-200 bg-red-50 p-6 shadow-sm dark:border-red-900/60 dark:bg-red-950/20 dark:shadow-none">
      <div className="text-sm font-semibold text-red-700 dark:text-red-300">
        Workflow could not be loaded.
      </div>
      <div className="mt-2 text-sm text-red-600 dark:text-red-200/70">
        {message}
      </div>
    </div>
  );
}

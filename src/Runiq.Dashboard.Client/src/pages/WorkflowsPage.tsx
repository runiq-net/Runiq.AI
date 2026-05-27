import { GitBranch, Search } from 'lucide-react';
import { useEffect, useMemo, useState, type ReactNode } from 'react';

import { getWorkflows, type WorkflowMetadata } from '../api/agentMetadataApi';
import { DataList, type DataListColumn } from '../components/DataList/DataList';
import { getDashboardBasePath } from '../dashboardConfig';

const workflowColumns: DataListColumn<WorkflowMetadata>[] = [
  {
    key: 'workflow',
    header: 'Workflow',
    width: 'minmax(240px, 1fr)',
    render: (workflow) => (
      <div className="flex min-w-0 items-center gap-3">
        <span className="inline-flex size-8 shrink-0 items-center justify-center rounded-xl border border-zinc-200 bg-zinc-50 text-zinc-600 dark:border-zinc-800 dark:bg-zinc-900 dark:text-zinc-300">
          <GitBranch className="size-4" />
        </span>

        <div className="min-w-0">
          <div className="truncate text-sm font-semibold text-zinc-950 dark:text-zinc-100">
            {workflow.name || workflow.id}
          </div>

          <div className="mt-0.5 truncate font-mono text-xs text-zinc-500 dark:text-zinc-500">
            {workflow.id}
          </div>
        </div>
      </div>
    ),
  },
  {
    key: 'steps',
    header: 'Steps',
    width: '96px',
    render: (workflow) => (
      <div className="text-sm font-medium text-zinc-700 dark:text-zinc-300">
        {workflow.steps.length}
      </div>
    ),
  },
  {
    key: 'start',
    header: 'Start',
    width: 'minmax(120px, 0.5fr)',
    render: (workflow) => (
      <StepBadge>{getStartStepId(workflow) ?? 'not configured'}</StepBadge>
    ),
  },
  {
    key: 'final',
    header: 'Final',
    width: 'minmax(120px, 0.5fr)',
    render: (workflow) => (
      <StepBadge>{getFinalStepId(workflow) ?? 'not configured'}</StepBadge>
    ),
  },
  {
    key: 'flow',
    header: 'Flow',
    width: 'minmax(360px, 2fr)',
    render: (workflow) => (
      <div
        className="truncate font-mono text-xs text-zinc-600 dark:text-zinc-400"
        title={formatWorkflowFlow(workflow)}
      >
        {formatWorkflowFlow(workflow)}
      </div>
    ),
  },
];

export function WorkflowsPage() {
  const [workflows, setWorkflows] = useState<WorkflowMetadata[]>([]);
  const [filter, setFilter] = useState('');
  const [isLoading, setLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  useEffect(() => {
    let isMounted = true;

    async function loadWorkflows() {
      try {
        setLoading(true);
        setErrorMessage(null);

        const result = await getWorkflows(getDashboardBasePath());

        if (isMounted) {
          setWorkflows(result);
        }
      } catch (error) {
        if (isMounted) {
          setErrorMessage(
            error instanceof Error
              ? error.message
              : 'Workflows metadata could not be loaded.',
          );
        }
      } finally {
        if (isMounted) {
          setLoading(false);
        }
      }
    }

    void loadWorkflows();

    return () => {
      isMounted = false;
    };
  }, []);

  const filteredWorkflows = useMemo(() => {
    const normalizedFilter = filter.trim().toLowerCase();

    if (!normalizedFilter) {
      return workflows;
    }

    return workflows.filter((workflow) => {
      return (
        workflow.id.toLowerCase().includes(normalizedFilter) ||
        workflow.name.toLowerCase().includes(normalizedFilter) ||
        workflow.steps.some((step) =>
          `${step.id} ${step.agentName} ${step.agentType}`
            .toLowerCase()
            .includes(normalizedFilter),
        )
      );
    });
  }, [filter, workflows]);

  if (isLoading) {
    return (
      <WorkflowsPageContainer>
        <WorkflowsLoadingState />
      </WorkflowsPageContainer>
    );
  }

  if (errorMessage) {
    return (
      <WorkflowsPageContainer>
        <WorkflowsErrorState message={errorMessage} />
      </WorkflowsPageContainer>
    );
  }

  return (
    <WorkflowsPageContainer>
      <WorkflowsSummary workflowsCount={workflows.length} />

      <div className="max-w-xl">
        <label className="relative block">
          <Search className="pointer-events-none absolute left-4 top-1/2 size-4 -translate-y-1/2 text-zinc-400 dark:text-zinc-600" />

          <input
            value={filter}
            onChange={(event) => setFilter(event.target.value)}
            placeholder="Filter by workflow or agent"
            className="h-11 w-full rounded-xl border border-zinc-200 bg-white pl-11 pr-4 text-sm text-zinc-950 outline-none transition placeholder:text-zinc-400 focus:border-zinc-400 dark:border-zinc-800 dark:bg-zinc-950/40 dark:text-zinc-100 dark:placeholder:text-zinc-600 dark:focus:border-zinc-600"
          />
        </label>
      </div>

      {workflows.length === 0 ? (
        <WorkflowsEmptyState />
      ) : filteredWorkflows.length === 0 ? (
        <div className="rounded-2xl border border-zinc-200 bg-white p-8 text-sm text-zinc-500 shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40 dark:text-zinc-500 dark:shadow-none">
          No workflows found.
        </div>
      ) : (
        <DataList
          rows={filteredWorkflows}
          columns={workflowColumns}
          getRowKey={(workflow) => workflow.id}
          onRowClick={(workflow) => {
            window.history.pushState(
              {},
              '',
              buildWorkflowDetailPath(getDashboardBasePath(), workflow.id),
            );
            window.dispatchEvent(new PopStateEvent('popstate'));
          }}
        />
      )}
    </WorkflowsPageContainer>
  );
}

function buildWorkflowDetailPath(basePath: string, workflowId: string): string {
  const normalizedBasePath = basePath.replace(/\/+$/g, '');

  return `${normalizedBasePath}/workflows/${encodeURIComponent(workflowId)}`;
}

function WorkflowsPageContainer({ children }: { children: ReactNode }) {
  return <div className="space-y-6">{children}</div>;
}

function WorkflowsSummary({ workflowsCount }: { workflowsCount: number }) {
  return (
    <div className="rounded-2xl border border-zinc-200 bg-white p-6 shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40 dark:shadow-none">
      <div className="flex items-center justify-between gap-4">
        <div className="flex min-w-0 items-start gap-3">
          <span className="inline-flex size-9 shrink-0 items-center justify-center rounded-xl border border-zinc-200 bg-zinc-50 text-zinc-700 dark:border-zinc-800 dark:bg-zinc-900 dark:text-zinc-300">
            <GitBranch className="size-4" />
          </span>

          <div className="min-w-0">
            <div className="text-sm font-semibold text-zinc-950 dark:text-zinc-100">
              Registered Workflows
            </div>

            <p className="mt-1 text-sm text-zinc-600 dark:text-zinc-400">
              Deterministic agent execution graphs registered in this runtime.
            </p>
          </div>
        </div>

        <div className="inline-flex shrink-0 items-center gap-2 rounded-full border border-zinc-200 bg-zinc-50 px-4 py-2 text-sm font-medium text-zinc-700 dark:border-zinc-800 dark:bg-zinc-900 dark:text-zinc-300">
          <span className="size-1.5 rounded-full bg-emerald-500" />
          {workflowsCount} workflow{workflowsCount === 1 ? '' : 's'}
        </div>
      </div>
    </div>
  );
}

function WorkflowsLoadingState() {
  return (
    <div className="rounded-2xl border border-zinc-200 bg-white p-6 shadow-sm dark:border-zinc-800 dark:bg-zinc-950/50 dark:shadow-none">
      <div className="h-4 w-36 animate-pulse rounded bg-zinc-200 dark:bg-zinc-800" />

      <div className="mt-4 space-y-3">
        <div className="h-14 animate-pulse rounded-xl bg-zinc-100 dark:bg-zinc-900/80" />
        <div className="h-14 animate-pulse rounded-xl bg-zinc-100 dark:bg-zinc-900/60" />
        <div className="h-14 animate-pulse rounded-xl bg-zinc-100 dark:bg-zinc-900/40" />
      </div>
    </div>
  );
}

function WorkflowsErrorState({ message }: { message: string }) {
  return (
    <div className="rounded-2xl border border-red-200 bg-red-50 p-6 shadow-sm dark:border-red-900/60 dark:bg-red-950/20 dark:shadow-none">
      <div className="text-sm font-medium text-red-700 dark:text-red-300">
        Workflows could not be loaded.
      </div>

      <div className="mt-2 text-sm text-red-600 dark:text-red-200/70">
        {message}
      </div>
    </div>
  );
}

function WorkflowsEmptyState() {
  return (
    <div className="rounded-2xl border border-zinc-200 bg-white p-8 text-center shadow-sm dark:border-zinc-800 dark:bg-zinc-950/50 dark:shadow-none">
      <div className="mx-auto flex size-11 items-center justify-center rounded-2xl border border-zinc-200 bg-zinc-50 text-zinc-500 dark:border-zinc-800 dark:bg-zinc-950 dark:text-zinc-400">
        <GitBranch size={20} strokeWidth={2} aria-hidden="true" />
      </div>

      <div className="mt-4 text-sm font-medium text-zinc-950 dark:text-zinc-100">
        No workflows registered.
      </div>

      <div className="mt-2 text-sm text-zinc-600 dark:text-zinc-500">
        Registered workflows will appear here.
      </div>
    </div>
  );
}

function StepBadge({ children }: { children: ReactNode }) {
  return (
    <span className="inline-flex max-w-full truncate rounded-full border border-zinc-200 bg-zinc-100 px-2.5 py-1 font-mono text-xs font-medium text-zinc-800 dark:border-zinc-800 dark:bg-zinc-950 dark:text-zinc-300">
      {children}
    </span>
  );
}

function getStartStepId(workflow: WorkflowMetadata): string | null {
  return workflow.steps[0]?.id ?? null;
}

function getFinalStepId(workflow: WorkflowMetadata): string | null {
  const flow = buildSuccessFlow(workflow);

  return flow.at(-1) ?? null;
}

function formatWorkflowFlow(workflow: WorkflowMetadata): string {
  const flow = buildSuccessFlow(workflow);

  if (flow.length === 0) {
    return 'No steps';
  }

  return flow.join(' -> ');
}

function buildSuccessFlow(workflow: WorkflowMetadata): string[] {
  const firstStep = workflow.steps[0];

  if (!firstStep) {
    return [];
  }

  const stepsById = new Map(
    workflow.steps.map((step) => [step.id.toLowerCase(), step]),
  );
  const visited = new Set<string>();
  const flow: string[] = [];
  let currentStep = firstStep;

  while (!visited.has(currentStep.id.toLowerCase())) {
    flow.push(currentStep.id);
    visited.add(currentStep.id.toLowerCase());

    if (!currentStep.successStepId) {
      break;
    }

    const nextStep = stepsById.get(currentStep.successStepId.toLowerCase());

    if (!nextStep) {
      flow.push(currentStep.successStepId);
      break;
    }

    currentStep = nextStep;
  }

  return flow;
}

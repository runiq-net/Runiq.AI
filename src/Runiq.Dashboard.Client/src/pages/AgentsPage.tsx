import { Bot, Search } from 'lucide-react';
import { useEffect, useMemo, useState, type ReactNode } from 'react';

import { getAgents, type AgentMetadata } from '../api/agentMetadataApi';
import { DataList, type DataListColumn } from '../components/DataList/DataList';
import { getDashboardBasePath } from '../dashboardConfig';

const agentColumns: DataListColumn<AgentMetadata>[] = [
  {
    key: 'agent',
    header: 'Agent',
    width: 'minmax(240px, 1fr)',
    render: (agent) => (
      <div className="flex min-w-0 items-center gap-3">
        <span className="inline-flex size-8 shrink-0 items-center justify-center rounded-xl border border-zinc-200 bg-zinc-50 text-zinc-600 dark:border-zinc-800 dark:bg-zinc-900 dark:text-zinc-300">
          <Bot className="size-4" />
        </span>

        <div className="min-w-0">
          <div className="truncate text-sm font-semibold text-zinc-950 transition group-hover:text-black dark:text-zinc-100 dark:group-hover:text-white">
            {agent.name || agent.id}
          </div>
        </div>
      </div>
    ),
  },
  {
    key: 'provider',
    header: 'Provider',
    width: 'minmax(140px, 0.7fr)',
    render: (agent) => (
      <span className="inline-flex max-w-full truncate rounded-full border border-zinc-200 bg-zinc-100 px-2.5 py-1 text-xs font-medium text-zinc-800 transition group-hover:border-zinc-300 group-hover:text-zinc-950 dark:border-zinc-800 dark:bg-zinc-950 dark:text-zinc-300 dark:group-hover:border-zinc-700 dark:group-hover:text-zinc-100">
        {getProvider(agent.model)}
      </span>
    ),
  },
  {
    key: 'model',
    header: 'Model',
    width: 'minmax(160px, 0.8fr)',
    render: (agent) => (
      <span className="inline-flex max-w-full truncate rounded-full border border-zinc-200 bg-zinc-50 px-2.5 py-1 text-xs text-zinc-700 transition group-hover:border-zinc-300 group-hover:text-zinc-950 dark:border-zinc-800 dark:bg-zinc-900/60 dark:text-zinc-400 dark:group-hover:border-zinc-700 dark:group-hover:text-zinc-200">
        {getModel(agent.model)}
      </span>
    ),
  },
  {
    key: 'instructions',
    header: 'Instructions',
    width: 'minmax(360px, 2fr)',
    render: (agent) => {
      const instructions = agent.instructions || 'No instructions configured.';

      return (
        <div
          className="truncate text-sm text-zinc-600 transition group-hover:text-zinc-950 dark:text-zinc-500 dark:group-hover:text-zinc-300"
          title={instructions}
        >
          {truncateText(instructions, 72)}
        </div>
      );
    },
  },
];

export function AgentsPage() {
  const [agents, setAgents] = useState<AgentMetadata[]>([]);
  const [filter, setFilter] = useState('');
  const [isLoading, setLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  useEffect(() => {
    let isMounted = true;

    async function loadAgents() {
      try {
        setLoading(true);
        setErrorMessage(null);

        const basePath = getDashboardBasePath();
        const result = await getAgents(basePath);

        if (isMounted) {
          setAgents(result);
        }
      } catch (error) {
        if (isMounted) {
          setErrorMessage(
            error instanceof Error
              ? error.message
              : 'Agents metadata could not be loaded.',
          );
        }
      } finally {
        if (isMounted) {
          setLoading(false);
        }
      }
    }

    void loadAgents();

    return () => {
      isMounted = false;
    };
  }, []);

  const filteredAgents = useMemo(() => {
    const normalizedFilter = filter.trim().toLowerCase();

    if (!normalizedFilter) {
      return agents;
    }

    return agents.filter((agent) => {
      return (
        agent.id.toLowerCase().includes(normalizedFilter) ||
        agent.name.toLowerCase().includes(normalizedFilter) ||
        agent.model?.toLowerCase().includes(normalizedFilter) ||
        agent.instructions?.toLowerCase().includes(normalizedFilter)
      );
    });
  }, [agents, filter]);

  if (isLoading) {
    return (
      <AgentsPageContainer>
        <AgentsLoadingState />
      </AgentsPageContainer>
    );
  }

  if (errorMessage) {
    return (
      <AgentsPageContainer>
        <AgentsErrorState message={errorMessage} />
      </AgentsPageContainer>
    );
  }

  return (
    <AgentsPageContainer>
      <AgentsSummary agentsCount={agents.length} />

      <div className="max-w-xl">
        <label className="relative block">
          <Search className="pointer-events-none absolute left-4 top-1/2 size-4 -translate-y-1/2 text-zinc-400 dark:text-zinc-600" />

          <input
            value={filter}
            onChange={(event) => setFilter(event.target.value)}
            placeholder="Filter by name"
            className="h-11 w-full rounded-xl border border-zinc-200 bg-white pl-11 pr-4 text-sm text-zinc-950 outline-none transition placeholder:text-zinc-400 focus:border-zinc-400 dark:border-zinc-800 dark:bg-zinc-950/40 dark:text-zinc-100 dark:placeholder:text-zinc-600 dark:focus:border-zinc-600"
          />
        </label>
      </div>

      {agents.length === 0 ? (
        <AgentsEmptyState />
      ) : filteredAgents.length === 0 ? (
        <div className="rounded-2xl border border-zinc-200 bg-white p-8 text-sm text-zinc-500 shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40 dark:text-zinc-500 dark:shadow-none">
          No agents found.
        </div>
      ) : (
        <DataList
          rows={filteredAgents}
          columns={agentColumns}
          getRowKey={(agent) => agent.id}
          onRowClick={(agent) => {
            window.history.pushState(
              {},
              '',
              buildAgentChatPath(getDashboardBasePath(), agent.id),
            );

            window.dispatchEvent(new PopStateEvent('popstate'));
          }}
        />
      )}
    </AgentsPageContainer>
  );
}

function buildAgentChatPath(basePath: string, agentId: string): string {
  const normalizedBasePath = basePath.replace(/\/+$/g, '');

  return `${normalizedBasePath}/agents/${encodeURIComponent(agentId)}/chat/new`;
}

function AgentsPageContainer({ children }: { children: ReactNode }) {
  return <div className="space-y-6">{children}</div>;
}

function AgentsSummary({ agentsCount }: { agentsCount: number }) {
  return (
    <div className="rounded-2xl border border-zinc-200 bg-white p-6 shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40 dark:shadow-none">
      <div className="flex items-center justify-between gap-4">
        <div className="flex min-w-0 items-start gap-3">
          <span className="inline-flex size-9 shrink-0 items-center justify-center rounded-xl border border-zinc-200 bg-zinc-50 text-zinc-700 dark:border-zinc-800 dark:bg-zinc-900 dark:text-zinc-300">
            <Bot className="size-4" />
          </span>

          <div className="min-w-0">
            <div className="text-sm font-semibold text-zinc-950 dark:text-zinc-100">
              Registered Agents
            </div>

            <p className="mt-1 text-sm text-zinc-600 dark:text-zinc-400">
              Runtime-discovered agents available in this dashboard.
            </p>
          </div>
        </div>

        <div className="inline-flex shrink-0 items-center gap-2 rounded-full border border-zinc-200 bg-zinc-50 px-4 py-2 text-sm font-medium text-zinc-700 dark:border-zinc-800 dark:bg-zinc-900 dark:text-zinc-300">
          <span className="size-1.5 rounded-full bg-emerald-500" />
          {agentsCount} agent{agentsCount === 1 ? '' : 's'}
        </div>
      </div>
    </div>
  );
}

function AgentsLoadingState() {
  return (
    <div className="rounded-2xl border border-zinc-200 bg-white p-6 shadow-sm dark:border-zinc-800 dark:bg-zinc-950/50 dark:shadow-none">
      <div className="h-4 w-32 animate-pulse rounded bg-zinc-200 dark:bg-zinc-800" />

      <div className="mt-4 space-y-3">
        <div className="h-14 animate-pulse rounded-xl bg-zinc-100 dark:bg-zinc-900/80" />
        <div className="h-14 animate-pulse rounded-xl bg-zinc-100 dark:bg-zinc-900/60" />
        <div className="h-14 animate-pulse rounded-xl bg-zinc-100 dark:bg-zinc-900/40" />
      </div>
    </div>
  );
}

function AgentsErrorState({ message }: { message: string }) {
  return (
    <div className="rounded-2xl border border-red-200 bg-red-50 p-6 shadow-sm dark:border-red-900/60 dark:bg-red-950/20 dark:shadow-none">
      <div className="text-sm font-medium text-red-700 dark:text-red-300">
        Agents could not be loaded.
      </div>

      <div className="mt-2 text-sm text-red-600 dark:text-red-200/70">
        {message}
      </div>
    </div>
  );
}

function AgentsEmptyState() {
  return (
    <div className="rounded-2xl border border-zinc-200 bg-white p-8 text-center shadow-sm dark:border-zinc-800 dark:bg-zinc-950/50 dark:shadow-none">
      <div className="mx-auto flex size-11 items-center justify-center rounded-2xl border border-zinc-200 bg-zinc-50 text-zinc-500 dark:border-zinc-800 dark:bg-zinc-950 dark:text-zinc-400">
        <Bot size={20} strokeWidth={2} aria-hidden="true" />
      </div>

      <div className="mt-4 text-sm font-medium text-zinc-950 dark:text-zinc-100">
        No agents registered.
      </div>

      <div className="mt-2 text-sm text-zinc-600 dark:text-zinc-500">
        Registered agents will appear here.
      </div>
    </div>
  );
}

function getProvider(model: string | undefined): string {
  if (!model || !model.includes('/')) {
    return 'unknown';
  }

  return model.split('/')[0] || 'unknown';
}

function getModel(model: string | undefined): string {
  if (!model) {
    return 'not configured';
  }

  if (!model.includes('/')) {
    return model;
  }

  return model.split('/')[1] || model;
}

function truncateText(value: string, maxLength: number): string {
  if (value.length <= maxLength) {
    return value;
  }

  return `${value.slice(0, maxLength).trimEnd()}...`;
}
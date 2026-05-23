import { Search, Wrench } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';

import { getTools, type ToolMetadata } from '../api/agentMetadataApi';
import { DataList, type DataListColumn } from '../components/DataList/DataList';
import { getDashboardBasePath } from '../dashboardConfig';

const toolColumns: DataListColumn<ToolMetadata>[] = [
  {
    key: 'tool',
    header: 'Tool',
    width: 'minmax(240px, 1fr)',
    render: (tool) => (
      <div className="flex min-w-0 items-center gap-3">
        <span className="inline-flex size-8 shrink-0 items-center justify-center rounded-xl border border-zinc-200 bg-zinc-50 text-zinc-600 dark:border-zinc-800 dark:bg-zinc-900 dark:text-zinc-300">
          <Wrench className="size-4" />
        </span>

        <div className="min-w-0">
          <div className="truncate text-sm font-semibold text-zinc-950 dark:text-zinc-100">
            {tool.displayName || tool.name}
          </div>
        </div>
      </div>
    ),
  },
  {
    key: 'description',
    header: 'Description',
    width: 'minmax(360px, 2fr)',
    render: (tool) => (
      <div className="truncate text-sm text-zinc-600 dark:text-zinc-400">
        {tool.description || 'No description.'}
      </div>
    ),
  },
  {
    key: 'agents',
    header: 'Agents',
    width: '120px',
    render: (tool) => (
      <div className="text-sm font-medium text-zinc-700 dark:text-zinc-300">
        {tool.attachedAgents.length}
      </div>
    ),
  },
];

export function ToolsPage() {
  const [tools, setTools] = useState<ToolMetadata[]>([]);
  const [filter, setFilter] = useState('');
  const [isLoading, setLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  useEffect(() => {
    let isMounted = true;

    async function loadTools() {
      try {
        setLoading(true);
        setErrorMessage(null);

        const result = await getTools(getDashboardBasePath());

        if (!isMounted) {
          return;
        }

        setTools(result);
      } catch (error) {
        if (!isMounted) {
          return;
        }

        setErrorMessage(
          error instanceof Error ? error.message : 'Tools metadata could not be loaded.',
        );
      } finally {
        if (isMounted) {
          setLoading(false);
        }
      }
    }

    void loadTools();

    return () => {
      isMounted = false;
    };
  }, []);

  const filteredTools = useMemo(() => {
    const normalizedFilter = filter.trim().toLowerCase();

    if (!normalizedFilter) {
      return tools;
    }

    return tools.filter((tool) => {
      return (
        tool.name.toLowerCase().includes(normalizedFilter) ||
        tool.displayName.toLowerCase().includes(normalizedFilter) ||
        tool.description?.toLowerCase().includes(normalizedFilter) ||
        tool.typeName.toLowerCase().includes(normalizedFilter)
      );
    });
  }, [filter, tools]);

  if (isLoading) {
    return (
      <div className="rounded-2xl border border-zinc-200 bg-white p-6 text-sm text-zinc-500 shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40 dark:text-zinc-500 dark:shadow-none">
        Loading tools...
      </div>
    );
  }

  if (errorMessage) {
    return (
      <div className="rounded-2xl border border-red-200 bg-red-50 p-6 text-sm text-red-700 shadow-sm dark:border-red-900/60 dark:bg-red-950/20 dark:text-red-300 dark:shadow-none">
        {errorMessage}
      </div>
    );
  }

return (
  <div className="space-y-6">
    <div className="rounded-2xl border border-zinc-200 bg-white p-6 shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40 dark:shadow-none">
      <div className="flex items-center justify-between gap-4">
        <div className="flex min-w-0 items-start gap-3">
          <span className="inline-flex size-9 shrink-0 items-center justify-center rounded-xl border border-zinc-200 bg-zinc-50 text-zinc-700 dark:border-zinc-800 dark:bg-zinc-900 dark:text-zinc-300">
            <Wrench className="size-4" />
          </span>

          <div className="min-w-0">
            <div className="text-sm font-semibold text-zinc-950 dark:text-zinc-100">
              Registered Tools
            </div>

            <p className="mt-1 text-sm text-zinc-600 dark:text-zinc-400">
              Runtime-discovered tools available in this dashboard.
            </p>
          </div>
        </div>

        <div className="inline-flex shrink-0 items-center gap-2 rounded-full border border-zinc-200 bg-zinc-50 px-4 py-2 text-sm font-medium text-zinc-700 dark:border-zinc-800 dark:bg-zinc-900 dark:text-zinc-300">
          <span className="size-1.5 rounded-full bg-emerald-500" />
          {tools.length} tools
        </div>
      </div>
    </div>

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

    {filteredTools.length === 0 ? (
      <div className="rounded-2xl border border-zinc-200 bg-white p-8 text-sm text-zinc-500 shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40 dark:text-zinc-500 dark:shadow-none">
        No tools found.
      </div>
    ) : (
<DataList
  rows={filteredTools}
  columns={toolColumns}
  getRowKey={(tool) => tool.name}
  onRowClick={(tool) => {
    window.history.pushState(
      {},
      '',
      buildToolDetailPath(getDashboardBasePath(), tool.name),
    );

    window.dispatchEvent(new PopStateEvent('popstate'));
  }}
/>
    )}
  </div>
);
}

function buildToolDetailPath(basePath: string, toolName: string): string {
  const normalizedBasePath = basePath.replace(/\/+$/g, '');

  return `${normalizedBasePath}/tools/${encodeURIComponent(toolName)}`;
}
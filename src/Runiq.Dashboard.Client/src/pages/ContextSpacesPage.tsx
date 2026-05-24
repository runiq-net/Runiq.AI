import { Database, Search } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';

import {
    getContextSpaces,
    type ContextSpaceMetadata,
} from '../api/agentMetadataApi';
import { DataList, type DataListColumn } from '../components/DataList/DataList';
import { getDashboardBasePath } from '../dashboardConfig';

const contextSpaceColumns: DataListColumn<ContextSpaceMetadata>[] = [
    {
        key: 'context-space',
        header: 'Context Space',
        width: 'minmax(260px, 1fr)',
        render: (contextSpace) => (
            <div className="flex min-w-0 items-center gap-3">
                <span className="inline-flex size-8 shrink-0 items-center justify-center rounded-xl border border-zinc-200 bg-zinc-50 text-zinc-600 dark:border-zinc-800 dark:bg-zinc-900 dark:text-zinc-300">
                    <Database className="size-4" />
                </span>

                <div className="min-w-0">
                    <div className="truncate text-sm font-semibold text-zinc-950 dark:text-zinc-100">
                        {contextSpace.name || contextSpace.id}
                    </div>

                    <div className="mt-0.5 truncate text-xs text-zinc-500 dark:text-zinc-600">
                        {contextSpace.id}
                    </div>
                </div>
            </div>
        ),
    },
    {
        key: 'description',
        header: 'Description',
        width: 'minmax(360px, 2fr)',
        render: (contextSpace) => (
            <div
                className="truncate text-sm text-zinc-600 dark:text-zinc-400"
                title={contextSpace.description || undefined}
            >
                {contextSpace.description || 'No description.'}
            </div>
        ),
    },
    {
        key: 'sources',
        header: 'Sources',
        width: '120px',
        render: (contextSpace) => (
            <div className="text-sm font-medium text-zinc-700 dark:text-zinc-300">
                {contextSpace.sources.length}
            </div>
        ),
    },
    {
        key: 'agents',
        header: 'Agents',
        width: '120px',
        render: (contextSpace) => (
            <div className="text-sm font-medium text-zinc-700 dark:text-zinc-300">
                {contextSpace.attachedAgents.length}
            </div>
        ),
    },
];

export function ContextSpacesPage() {
    const [contextSpaces, setContextSpaces] = useState<ContextSpaceMetadata[]>([]);
    const [filter, setFilter] = useState('');
    const [isLoading, setLoading] = useState(true);
    const [errorMessage, setErrorMessage] = useState<string | null>(null);

    useEffect(() => {
        let isMounted = true;

        async function loadContextSpaces() {
            try {
                setLoading(true);
                setErrorMessage(null);

                const result = await getContextSpaces(getDashboardBasePath());

                if (!isMounted) {
                    return;
                }

                setContextSpaces(result);
            } catch (error) {
                if (!isMounted) {
                    return;
                }

                setErrorMessage(
                    error instanceof Error
                        ? error.message
                        : 'Context Spaces metadata could not be loaded.',
                );
            } finally {
                if (isMounted) {
                    setLoading(false);
                }
            }
        }

        void loadContextSpaces();

        return () => {
            isMounted = false;
        };
    }, []);

    const filteredContextSpaces = useMemo(() => {
        const normalizedFilter = filter.trim().toLowerCase();

        if (!normalizedFilter) {
            return contextSpaces;
        }

        return contextSpaces.filter((contextSpace) => {
            return (
                contextSpace.id.toLowerCase().includes(normalizedFilter) ||
                contextSpace.name.toLowerCase().includes(normalizedFilter) ||
                contextSpace.description?.toLowerCase().includes(normalizedFilter) ||
                contextSpace.sources.some((source) =>
                    `${source.id} ${source.name} ${source.kind} ${source.description ?? ''}`
                        .toLowerCase()
                        .includes(normalizedFilter),
                )
            );
        });
    }, [contextSpaces, filter]);

    if (isLoading) {
        return (
            <div className="rounded-2xl border border-zinc-200 bg-white p-6 text-sm text-zinc-500 shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40 dark:text-zinc-500 dark:shadow-none">
                Loading context spaces...
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
                            <Database className="size-4" />
                        </span>

                        <div className="min-w-0">
                            <div className="text-sm font-semibold text-zinc-950 dark:text-zinc-100">
                                Context Spaces
                            </div>

                            <p className="mt-1 text-sm text-zinc-600 dark:text-zinc-400">
                                Runtime context boundaries attached to agents.
                            </p>
                        </div>
                    </div>

                    <div className="inline-flex shrink-0 items-center gap-2 rounded-full border border-zinc-200 bg-zinc-50 px-4 py-2 text-sm font-medium text-zinc-700 dark:border-zinc-800 dark:bg-zinc-900 dark:text-zinc-300">
                        <span className="size-1.5 rounded-full bg-emerald-500" />
                        {contextSpaces.length} context space
                        {contextSpaces.length === 1 ? '' : 's'}
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

            {contextSpaces.length === 0 ? (
                <ContextSpacesEmptyState />
            ) : filteredContextSpaces.length === 0 ? (
                <div className="rounded-2xl border border-zinc-200 bg-white p-8 text-sm text-zinc-500 shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40 dark:text-zinc-500 dark:shadow-none">
                    No context spaces found.
                </div>
            ) : (
                <DataList
                    rows={filteredContextSpaces}
                    columns={contextSpaceColumns}
                    getRowKey={(contextSpace) => contextSpace.id}
                    onRowClick={(contextSpace) => {
                        window.history.pushState(
                            {},
                            '',
                            buildContextSpaceDetailPath(getDashboardBasePath(), contextSpace.id),
                        );

                        window.dispatchEvent(new PopStateEvent('popstate'));
                    }}
                />
            )}
        </div>
    );
}

function buildContextSpaceDetailPath(
  basePath: string,
  contextSpaceId: string,
): string {
  const normalizedBasePath = basePath.replace(/\/+$/g, '');

  return `${normalizedBasePath}/context-spaces/${encodeURIComponent(contextSpaceId)}`;
}

function ContextSpacesEmptyState() {
    return (
        <div className="rounded-2xl border border-zinc-200 bg-white p-8 text-center shadow-sm dark:border-zinc-800 dark:bg-zinc-950/50 dark:shadow-none">
            <div className="mx-auto flex size-11 items-center justify-center rounded-2xl border border-zinc-200 bg-zinc-50 text-zinc-500 dark:border-zinc-800 dark:bg-zinc-950 dark:text-zinc-400">
                <Database size={20} strokeWidth={2} aria-hidden="true" />
            </div>

            <div className="mt-4 text-sm font-medium text-zinc-950 dark:text-zinc-100">
                No context spaces registered.
            </div>

            <div className="mt-2 text-sm text-zinc-600 dark:text-zinc-500">
                Registered context spaces will appear here.
            </div>
        </div>
    );
}
import { Bot } from 'lucide-react';
import { useState, type ReactNode } from 'react';

import type { ToolMetadata } from '../../api/agentMetadataApi';
import { ToolSchemaTab } from './ToolSchemaTab';
import { getDashboardBasePath } from '../../dashboardConfig';


type ToolInspectorPanelProps = {
    tool: ToolMetadata;
};

type ToolInspectorTab = 'overview' | 'schema';

const tabs: Array<{
    key: ToolInspectorTab;
    label: string;
}> = [
        { key: 'overview', label: 'Overview' },
        { key: 'schema', label: 'Schema' },
    ];

export function ToolInspectorPanel({ tool }: ToolInspectorPanelProps) {
    const [activeTab, setActiveTab] = useState<ToolInspectorTab>('overview');

    return (
        <aside className="hidden w-[320px] shrink-0 rounded-lg border border-zinc-200 bg-white shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40 dark:shadow-none xl:flex xl:min-h-0 xl:flex-col">
            <div className="border-b border-zinc-200 px-3 py-3 dark:border-zinc-800">
                <div className="truncate text-sm font-semibold text-zinc-950 dark:text-zinc-100">
                    {tool.displayName || tool.name}
                </div>

                <div className="mt-2 grid grid-cols-2 rounded-md bg-zinc-100 p-1 text-xs font-medium dark:bg-zinc-900">
                    {tabs.map((tab) => (
                        <button
                            key={tab.key}
                            type="button"
                            onClick={() => setActiveTab(tab.key)}
                            className={[
                                'rounded px-2 py-1 transition',
                                activeTab === tab.key
                                    ? 'bg-white text-zinc-950 shadow-sm dark:bg-zinc-800 dark:text-zinc-100'
                                    : 'text-zinc-500 hover:text-zinc-950 dark:text-zinc-500 dark:hover:text-zinc-200',
                            ].join(' ')}
                        >
                            {tab.label}
                        </button>
                    ))}
                </div>
            </div>

            <div className="min-h-0 flex-1 overflow-hidden p-3">
                {activeTab === 'overview' && <ToolOverviewTab tool={tool} />}
                {activeTab === 'schema' && <ToolSchemaTab tool={tool} />}
            </div>
        </aside>
    );
}

function ToolOverviewTab({ tool }: { tool: ToolMetadata }) {
    return (
        <div className="flex min-h-0 flex-col gap-3">
            <ToolDescriptionCard tool={tool} />

            <ToolTypesCard tool={tool} />

            <ToolAttachedAgentsCard tool={tool} />
        </div>
    );
}

function ToolDescriptionCard({ tool }: { tool: ToolMetadata }) {
    return (
        <InspectorCard title="Description">
            <p className="text-sm leading-6 text-zinc-600 dark:text-zinc-400">
                {tool.description || 'No description.'}
            </p>
        </InspectorCard>
    );
}

function ToolTypesCard({ tool }: { tool: ToolMetadata }) {
    return (
        <InspectorCard title="Types">
            <div className="space-y-3 text-sm">
                <InspectorRow label="Input">
                    <span className="truncate text-right font-medium text-zinc-800 dark:text-zinc-200">
                        {tool.inputType}
                    </span>
                </InspectorRow>

                <InspectorRow label="Output">
                    <span className="truncate text-right font-medium text-zinc-800 dark:text-zinc-200">
                        {tool.outputType}
                    </span>
                </InspectorRow>
            </div>
        </InspectorCard>
    );
}

function ToolAttachedAgentsCard({ tool }: { tool: ToolMetadata }) {
    return (
        <InspectorCard title="Attached agents">
            {tool.attachedAgents.length === 0 ? (
                <span className="text-sm font-medium text-zinc-800 dark:text-zinc-200">
                    No agents attached
                </span>
            ) : (
                <div className="space-y-2">
                    {tool.attachedAgents.map((agent) => (
                        <button
                            key={agent.id}
                            type="button"
                            onClick={() => navigateToAgent(agent.id)}
                            className="flex w-full items-center gap-3 rounded-xl border border-zinc-200 bg-white px-3 py-2 text-left transition hover:border-zinc-300 hover:bg-zinc-100 dark:border-zinc-700 dark:bg-zinc-900 dark:hover:border-zinc-600 dark:hover:bg-zinc-800"
                        >
                            <span className="inline-flex size-7 items-center justify-center rounded-lg border border-zinc-200 bg-zinc-50 text-zinc-600 dark:border-zinc-800 dark:bg-zinc-950 dark:text-zinc-300">
                                <Bot className="size-3.5" />
                            </span>

                            <div className="min-w-0">
                                <div className="truncate text-sm font-medium text-zinc-950 dark:text-zinc-100">
                                    {agent.name}
                                </div>

                                <div className="truncate font-mono text-xs text-zinc-500 dark:text-zinc-500">
                                    {agent.id}
                                </div>
                            </div>
                        </button>
                    ))}
                </div>
            )}
        </InspectorCard>
    );
}

function InspectorCard({
    title,
    children,
    className = '',
}: {
    title: string;
    children: ReactNode;
    className?: string;
}) {
    return (
        <section
            className={[
                'rounded-md border border-zinc-200 bg-zinc-50 p-3 dark:border-zinc-800 dark:bg-zinc-900/40',
                className,
            ].join(' ')}
        >
            <div className="mb-3 text-xs font-semibold uppercase tracking-[0.14em] text-zinc-500 dark:text-zinc-500">
                {title}
            </div>

            {children}
        </section>
    );
}

function InspectorRow({
    label,
    children,
}: {
    label: string;
    children: ReactNode;
}) {
    return (
        <div className="flex items-center justify-between gap-4">
            <span className="text-zinc-500 dark:text-zinc-500">{label}</span>
            <div className="min-w-0 text-right">{children}</div>
        </div>
    );
}

function navigateToAgent(agentId: string) {
  const basePath = getDashboardBasePath().replace(/\/+$/g, '');

  window.history.pushState(
    {},
    '',
    `${basePath}/agents/${encodeURIComponent(agentId)}/chat/new`,
  );

  window.dispatchEvent(new PopStateEvent('popstate'));
}
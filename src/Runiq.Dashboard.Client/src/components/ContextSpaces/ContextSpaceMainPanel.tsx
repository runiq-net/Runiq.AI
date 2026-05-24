import { useMemo, useState } from 'react';

import type { ContextSpaceMetadata } from '../../api/agentMetadataApi';
import { ContextSpaceOverviewTab } from './tabs/ContextSpaceOverviewTab';
import { ContextSpaceSkillsTab } from './tabs/ContextSpaceSkillsTab';
import { ContextSpaceSourcesTab } from './tabs/ContextSpaceSourcesTab';

type ContextSpaceMainPanelProps = {
  contextSpace: ContextSpaceMetadata;
};

type ContextSpaceMainTab = 'overview' | 'skills' | 'sources';

const tabs: Array<{
  key: ContextSpaceMainTab;
  label: string;
}> = [
  { key: 'overview', label: 'Overview' },
  { key: 'skills', label: 'Skills' },
  { key: 'sources', label: 'Sources' },
];

const tabTitles: Record<ContextSpaceMainTab, string> = {
  overview: 'Context Overview',
  skills: 'Skills',
  sources: 'Sources',
};

export function ContextSpaceMainPanel({
  contextSpace,
}: ContextSpaceMainPanelProps) {
  const [activeTab, setActiveTab] = useState<ContextSpaceMainTab>('overview');

  const activeTitle = useMemo(() => tabTitles[activeTab], [activeTab]);

  return (
    <section className="flex min-h-0 min-w-0 flex-1 flex-col rounded-lg border border-zinc-200 bg-white shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40 dark:shadow-none">
      <div className="border-b border-zinc-200 px-5 py-4 dark:border-zinc-800">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div className="min-w-0">
            <h1 className="truncate text-sm font-semibold text-zinc-950 dark:text-zinc-100">
              {activeTitle}
            </h1>
            <p className="mt-1 text-sm leading-6 text-zinc-600 dark:text-zinc-400">
              {contextSpace.name || contextSpace.id}
            </p>
          </div>

          <div className="inline-flex w-full rounded-md bg-zinc-100 p-1 text-sm font-medium dark:bg-zinc-900 sm:w-auto">
            {tabs.map((tab) => (
              <button
                key={tab.key}
                type="button"
                onClick={() => setActiveTab(tab.key)}
                className={[
                  'flex-1 rounded px-3 py-1.5 transition sm:flex-none',
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
      </div>

      <div className="min-h-0 flex-1 overflow-y-auto px-5 py-5 [scrollbar-width:thin] [scrollbar-color:rgb(161_161_170)_transparent] dark:[scrollbar-color:rgb(82_82_91)_transparent] [&::-webkit-scrollbar]:w-1.5 [&::-webkit-scrollbar-track]:bg-transparent [&::-webkit-scrollbar-thumb]:rounded-full [&::-webkit-scrollbar-thumb]:bg-zinc-300 dark:[&::-webkit-scrollbar-thumb]:bg-zinc-700">
        {activeTab === 'overview' ? (
          <ContextSpaceOverviewTab contextSpace={contextSpace} />
        ) : null}

        {activeTab === 'skills' ? (
          <ContextSpaceSkillsTab contextSpace={contextSpace} />
        ) : null}

        {activeTab === 'sources' ? (
          <ContextSpaceSourcesTab contextSpace={contextSpace} />
        ) : null}
      </div>
    </section>
  );
}

import { Bot, Database, Sparkles } from 'lucide-react';
import type { ReactNode } from 'react';

import type { ContextSpaceMetadata } from '../../../api/agentMetadataApi';

type ContextSpaceOverviewTabProps = {
  contextSpace: ContextSpaceMetadata;
};

export function ContextSpaceOverviewTab({
  contextSpace,
}: ContextSpaceOverviewTabProps) {
  return (
    <div className="space-y-5">
      <div className="rounded-lg border border-zinc-200 bg-zinc-50 p-5 dark:border-zinc-800 dark:bg-zinc-900/30">
        <div className="flex items-start gap-4">
          <span className="inline-flex size-10 shrink-0 items-center justify-center rounded-lg border border-zinc-200 bg-white text-zinc-700 dark:border-zinc-800 dark:bg-zinc-950 dark:text-zinc-300">
            <Database className="size-5" />
          </span>

          <div className="min-w-0">
            <h2 className="text-base font-semibold text-zinc-950 dark:text-zinc-100">
              {contextSpace.name || contextSpace.id}
            </h2>

            <div className="mt-1 break-all font-mono text-xs text-zinc-500 dark:text-zinc-500">
              {contextSpace.id}
            </div>

            <p className="mt-4 max-w-3xl text-sm leading-6 text-zinc-600 dark:text-zinc-400">
              {contextSpace.description || 'No description configured.'}
            </p>

            <p className="mt-3 max-w-3xl text-sm leading-6 text-zinc-600 dark:text-zinc-400">
              This context space provides reusable skills and sources to
              attached agents.
            </p>
          </div>
        </div>
      </div>

      <div className="grid gap-3 sm:grid-cols-3">
        <ContextMetric
          icon={<Sparkles className="size-4" />}
          label="Skills"
          value={contextSpace.skills.length}
        />
        <ContextMetric
          icon={<Database className="size-4" />}
          label="Sources"
          value={contextSpace.sources.length}
        />
        <ContextMetric
          icon={<Bot className="size-4" />}
          label="Attached Agents"
          value={contextSpace.attachedAgents.length}
        />
      </div>
    </div>
  );
}

function ContextMetric({
  icon,
  label,
  value,
}: {
  icon: ReactNode;
  label: string;
  value: number;
}) {
  return (
    <div className="rounded-md border border-zinc-200 bg-white px-4 py-3 dark:border-zinc-800 dark:bg-zinc-950/50">
      <div className="flex items-center justify-between gap-3">
        <span className="text-xs font-medium uppercase tracking-[0.12em] text-zinc-500 dark:text-zinc-500">
          {label}
        </span>
        <span className="inline-flex size-7 shrink-0 items-center justify-center rounded-md border border-zinc-200 bg-zinc-50 text-zinc-600 dark:border-zinc-800 dark:bg-zinc-900 dark:text-zinc-400">
          {icon}
        </span>
      </div>

      <div className="mt-2 text-lg font-semibold text-zinc-950 dark:text-zinc-100">
        {value}
      </div>
    </div>
  );
}

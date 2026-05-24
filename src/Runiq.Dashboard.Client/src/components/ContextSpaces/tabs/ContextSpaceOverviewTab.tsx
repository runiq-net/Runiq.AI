import { Bot, CheckCircle2, Database, FileSearch, FileText, Sparkles } from 'lucide-react';
import type { ReactNode } from 'react';

import type { ContextSpaceMetadata } from '../../../api/agentMetadataApi';

type ContextSpaceOverviewTabProps = {
  contextSpace: ContextSpaceMetadata;
  documentCount?: number;
};

export function ContextSpaceOverviewTab({
  contextSpace,
  documentCount,
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
              This context space provides reusable skills and searchable source
              documents to attached agents.
            </p>
          </div>
        </div>
      </div>

      <div className="grid gap-3 sm:grid-cols-4">
        <ContextMetric
          icon={<Sparkles className="size-4" />}
          label="Skill"
          value={contextSpace.skills.length}
        />
        <ContextMetric
          icon={<Database className="size-4" />}
          label="Source group"
          value={contextSpace.sources.length}
        />
        <ContextMetric
          icon={<FileText className="size-4" />}
          label="Document"
          value={documentCount}
        />
        <ContextMetric
          icon={<Bot className="size-4" />}
          label="Attached agent"
          value={contextSpace.attachedAgents.length}
        />
      </div>

      <section className="rounded-lg border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-950/50">
        <div className="mb-3 text-xs font-semibold uppercase tracking-[0.12em] text-zinc-500 dark:text-zinc-500">
          Runtime contribution
        </div>

        <div className="grid gap-2 lg:grid-cols-2">
          <RuntimeContributionItem
            icon={<Sparkles className="size-4" />}
            text={`Loads ${formatCount(contextSpace.skills.length, 'skill')} into model instructions`}
          />
          <RuntimeContributionItem
            icon={<FileText className="size-4" />}
            text={`Exposes ${formatCount(documentCount, 'searchable document')} from ${formatCount(contextSpace.sources.length, 'source group')}`}
          />
          <RuntimeContributionItem
            icon={<Bot className="size-4" />}
            text={`Used by ${formatCount(contextSpace.attachedAgents.length, 'attached agent')}`}
          />
          <RuntimeContributionItem
            icon={<FileSearch className="size-4" />}
            text="Supports source selection during agent runs"
          />
        </div>
      </section>
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
  value?: number;
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
        {value ?? '...'}
      </div>
    </div>
  );
}

function RuntimeContributionItem({
  icon,
  text,
}: {
  icon: ReactNode;
  text: string;
}) {
  return (
    <div className="flex min-w-0 items-center gap-3 rounded-md border border-zinc-200 bg-zinc-50 px-3 py-2.5 dark:border-zinc-800 dark:bg-zinc-900/40">
      <span className="inline-flex size-7 shrink-0 items-center justify-center rounded-md border border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-900/60 dark:bg-emerald-950/30 dark:text-emerald-300">
        {icon}
      </span>

      <span className="min-w-0 text-sm text-zinc-700 dark:text-zinc-300">
        {text}
      </span>

      <CheckCircle2 className="ml-auto size-4 shrink-0 text-emerald-600 dark:text-emerald-300" />
    </div>
  );
}

function formatCount(value: number | undefined, label: string): string {
  if (value === undefined) {
    return `... ${label}s`;
  }

  return `${value} ${label}${value === 1 ? '' : 's'}`;
}

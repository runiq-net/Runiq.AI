import { FileText } from 'lucide-react';

import type { ContextSpaceMetadata } from '../../../api/agentMetadataApi';

type ContextSpaceSourcesTabProps = {
  contextSpace: ContextSpaceMetadata;
};

export function ContextSpaceSourcesTab({
  contextSpace,
}: ContextSpaceSourcesTabProps) {
  if (contextSpace.sources.length === 0) {
    return <EmptyState message="No sources attached." />;
  }

  return (
    <div className="grid gap-3 lg:grid-cols-2">
      {contextSpace.sources.map((source) => (
        <article
          key={source.id}
          className="rounded-lg border border-zinc-200 bg-zinc-50 p-4 dark:border-zinc-800 dark:bg-zinc-900/30"
        >
          <div className="flex items-start gap-3">
            <span className="inline-flex size-9 shrink-0 items-center justify-center rounded-lg border border-zinc-200 bg-white text-zinc-700 dark:border-zinc-800 dark:bg-zinc-950 dark:text-zinc-300">
              <FileText className="size-4" />
            </span>

            <div className="min-w-0 flex-1">
              <div className="flex flex-wrap items-center gap-2">
                <h2 className="text-sm font-semibold text-zinc-950 dark:text-zinc-100">
                  {source.name || source.id}
                </h2>

                <span className="rounded-md border border-zinc-200 bg-white px-2 py-0.5 text-xs text-zinc-600 dark:border-zinc-800 dark:bg-zinc-950 dark:text-zinc-400">
                  {formatSourceKind(source.kind)}
                </span>
              </div>

              <p className="mt-2 text-sm leading-6 text-zinc-600 dark:text-zinc-400">
                {source.description || 'No description.'}
              </p>
            </div>
          </div>
        </article>
      ))}
    </div>
  );
}

function EmptyState({ message }: { message: string }) {
  return (
    <div className="rounded-lg border border-zinc-200 bg-zinc-50 p-5 text-sm text-zinc-600 dark:border-zinc-800 dark:bg-zinc-900/30 dark:text-zinc-400">
      {message}
    </div>
  );
}

function formatSourceKind(value: string): string {
  return value
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    .replace(/[-_]+/g, ' ')
    .trim();
}

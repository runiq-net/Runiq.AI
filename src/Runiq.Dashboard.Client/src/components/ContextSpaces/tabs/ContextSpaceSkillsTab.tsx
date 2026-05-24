import { Sparkles } from 'lucide-react';

import type { ContextSpaceMetadata } from '../../../api/agentMetadataApi';

type ContextSpaceSkillsTabProps = {
  contextSpace: ContextSpaceMetadata;
};

export function ContextSpaceSkillsTab({
  contextSpace,
}: ContextSpaceSkillsTabProps) {
  if (contextSpace.skills.length === 0) {
    return <EmptyState message="No skills discovered." />;
  }

  return (
    <div className="grid gap-3 lg:grid-cols-2">
      {contextSpace.skills.map((skill) => (
        <article
          key={`${skill.sourceId}:${skill.relativePath}`}
          className="rounded-lg border border-zinc-200 bg-zinc-50 p-4 dark:border-zinc-800 dark:bg-zinc-900/30"
        >
          <div className="flex items-start gap-3">
            <span className="inline-flex size-9 shrink-0 items-center justify-center rounded-lg border border-zinc-200 bg-white text-zinc-700 dark:border-zinc-800 dark:bg-zinc-950 dark:text-zinc-300">
              <Sparkles className="size-4" />
            </span>

            <div className="min-w-0 flex-1">
              <div className="flex flex-wrap items-center gap-2">
                <h2 className="text-sm font-semibold text-zinc-950 dark:text-zinc-100">
                  {skill.name || skill.id}
                </h2>

                {skill.version ? (
                  <span className="rounded-md border border-zinc-200 bg-white px-2 py-0.5 text-xs text-zinc-600 dark:border-zinc-800 dark:bg-zinc-950 dark:text-zinc-400">
                    v{skill.version}
                  </span>
                ) : null}
              </div>

              <p className="mt-2 text-sm leading-6 text-zinc-600 dark:text-zinc-400">
                {skill.description || 'No description.'}
              </p>

              {skill.tags.length > 0 ? (
                <div className="mt-3 flex flex-wrap gap-1.5">
                  {skill.tags.map((tag) => (
                    <span
                      key={tag}
                      className="rounded-full border border-zinc-200 bg-white px-2 py-0.5 text-xs text-zinc-600 dark:border-zinc-800 dark:bg-zinc-950 dark:text-zinc-400"
                    >
                      {tag}
                    </span>
                  ))}
                </div>
              ) : null}

              {skill.relativePath ? (
                <div className="mt-3 break-all font-mono text-xs text-zinc-500 dark:text-zinc-500">
                  {skill.relativePath}
                </div>
              ) : null}
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

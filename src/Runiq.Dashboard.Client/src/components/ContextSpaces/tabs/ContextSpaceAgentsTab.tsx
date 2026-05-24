import { Bot, ExternalLink } from 'lucide-react';

import type { ContextSpaceMetadata } from '../../../api/agentMetadataApi';
import { getDashboardBasePath } from '../../../dashboardConfig';

type ContextSpaceAgentsTabProps = {
  contextSpace: ContextSpaceMetadata;
};

export function ContextSpaceAgentsTab({
  contextSpace,
}: ContextSpaceAgentsTabProps) {
  return (
    <div className="space-y-3">
      <div className="rounded-lg border border-zinc-200 bg-white px-4 py-3 text-sm text-zinc-600 dark:border-zinc-800 dark:bg-zinc-950/50 dark:text-zinc-400">
        Agents using this context space can load its skills and search its
        documents at runtime.
      </div>

      {contextSpace.attachedAgents.length === 0 ? (
        <EmptyState message="No agents attached." />
      ) : (
        <div className="grid gap-3 lg:grid-cols-2">
          {contextSpace.attachedAgents.map((agent) => (
            <article
              key={agent.id}
              className="rounded-lg border border-zinc-200 bg-zinc-50 p-4 dark:border-zinc-800 dark:bg-zinc-900/30"
            >
              <div className="flex items-start gap-3">
                <span className="inline-flex size-9 shrink-0 items-center justify-center rounded-lg border border-zinc-200 bg-white text-zinc-700 dark:border-zinc-800 dark:bg-zinc-950 dark:text-zinc-300">
                  <Bot className="size-4" />
                </span>

                <div className="min-w-0 flex-1">
                  <div className="truncate text-sm font-semibold text-zinc-950 dark:text-zinc-100">
                    {agent.name || agent.id}
                  </div>

                  <div className="mt-1 truncate font-mono text-xs text-zinc-500 dark:text-zinc-500">
                    {agent.id}
                  </div>

                  <button
                    type="button"
                    onClick={() => navigateToAgent(agent.id)}
                    className="mt-3 inline-flex items-center gap-1.5 rounded-md border border-zinc-200 bg-white px-2.5 py-1.5 text-xs font-medium text-zinc-700 transition hover:border-zinc-300 hover:bg-zinc-100 hover:text-zinc-950 dark:border-zinc-800 dark:bg-zinc-950 dark:text-zinc-300 dark:hover:border-zinc-700 dark:hover:bg-zinc-900 dark:hover:text-zinc-100"
                  >
                    Open Playground
                    <ExternalLink className="size-3.5" />
                  </button>
                </div>
              </div>
            </article>
          ))}
        </div>
      )}
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

function navigateToAgent(agentId: string) {
  const basePath = getDashboardBasePath().replace(/\/+$/g, '');

  window.history.pushState(
    {},
    '',
    `${basePath}/agents/${encodeURIComponent(agentId)}/chat/new`,
  );

  window.dispatchEvent(new PopStateEvent('popstate'));
}

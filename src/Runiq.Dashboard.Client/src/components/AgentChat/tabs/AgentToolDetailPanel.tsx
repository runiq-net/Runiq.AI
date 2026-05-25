import { ArrowLeft, ExternalLink, Wrench } from 'lucide-react';

import type { AgentToolMetadata } from '../../../api/agentMetadataApi';
import { getDashboardBasePath } from '../../../dashboardConfig';
import { getToolDisplayName } from './agentToolDisplay';

type AgentToolDetailPanelProps = {
  tool: AgentToolMetadata;
  onBack: () => void;
};

export function AgentToolDetailPanel({
  tool,
  onBack,
}: AgentToolDetailPanelProps) {
  return (
    <div className="flex min-h-0 flex-col">
      <button
        type="button"
        onClick={onBack}
        className="mb-3 inline-flex items-center gap-1.5 self-start text-sm font-medium text-zinc-500 transition hover:text-zinc-950 dark:text-zinc-400 dark:hover:text-zinc-100"
      >
        <ArrowLeft className="size-4" />
        Back
      </button>

      <div className="rounded-md border border-zinc-200 bg-zinc-50 p-3 dark:border-zinc-800 dark:bg-zinc-900/40">
        <div className="flex items-start gap-2">
          <div className="rounded-md border border-zinc-200 bg-white p-1.5 text-zinc-500 dark:border-zinc-700 dark:bg-zinc-950 dark:text-zinc-400">
            <Wrench className="size-4" />
          </div>

          <div className="min-w-0">
            <div className="truncate text-sm font-semibold text-zinc-950 dark:text-zinc-100">
              {getToolDisplayName(tool)}
            </div>
            <div className="mt-1 text-xs text-zinc-500 dark:text-zinc-500">
              Tool
            </div>
          </div>
        </div>

        <div className="mt-4 space-y-4">
          <DetailSection title="Description">
            {formatDescription(tool.description)}
          </DetailSection>

          <DetailSection title="Input">
            {tool.inputType || 'Unknown'}
          </DetailSection>

          <DetailSection title="Output">
            {tool.outputType || 'Unknown'}
          </DetailSection>

          <button
            type="button"
            onClick={() => navigateToTool(tool.name)}
            className="inline-flex items-center gap-1.5 rounded-md border border-zinc-200 bg-white px-2.5 py-1.5 text-xs font-medium text-zinc-700 transition hover:border-zinc-300 hover:bg-zinc-50 hover:text-zinc-950 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-zinc-400 focus-visible:ring-offset-2 focus-visible:ring-offset-zinc-50 dark:border-zinc-700 dark:bg-zinc-950 dark:text-zinc-200 dark:hover:border-zinc-600 dark:hover:bg-zinc-900 dark:hover:text-white dark:focus-visible:ring-zinc-600 dark:focus-visible:ring-offset-zinc-900"
          >
            Open Playground
            <ExternalLink className="size-3.5" />
          </button>
        </div>
      </div>
    </div>
  );
}

function navigateToTool(toolName: string) {
  const basePath = getDashboardBasePath().replace(/\/+$/g, '');

  window.history.pushState(
    {},
    '',
    `${basePath}/tools/${encodeURIComponent(toolName)}`,
  );

  window.dispatchEvent(new PopStateEvent('popstate'));
}

function DetailSection({
  title,
  children,
}: {
  title: string;
  children: React.ReactNode;
}) {
  return (
    <section>
      <div className="text-xs font-semibold uppercase tracking-[0.14em] text-zinc-500 dark:text-zinc-500">
        {title}
      </div>
      <div className="mt-1 break-words text-sm leading-6 text-zinc-800 dark:text-zinc-200">
        {children}
      </div>
    </section>
  );
}

function formatDescription(description: string | undefined): string {
  const trimmedDescription = description?.trim();

  if (!trimmedDescription) {
    return 'No description provided.';
  }

  return trimmedDescription;
}

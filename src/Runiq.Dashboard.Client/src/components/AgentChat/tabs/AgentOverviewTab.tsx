import type { ReactNode } from 'react';
import { Database, Users, Wrench } from 'lucide-react';

import type {
  AgentContextSpaceMetadata,
  AgentMetadata,
  AgentToolMetadata,
} from '../../../api/agentMetadataApi';
import { getDashboardBasePath } from '../../../dashboardConfig';
import { parseModelReference } from '../../../utils/modelReference';
import { getToolDisplayName } from './agentToolDisplay';

type AgentOverviewTabProps = {
  agent: AgentMetadata;
  onOpenTools: () => void;
  onOpenTool: (toolName: string) => void;
};

export function AgentOverviewTab({
  agent,
  onOpenTools,
  onOpenTool,
}: AgentOverviewTabProps) {
  const modelReference = parseModelReference(agent.model);
  const tools = agent.tools ?? [];
  const contextSpaces = agent.contextSpaces ?? [];

  return (
    <div className="flex min-h-0 flex-col gap-2">
      <InspectorCard title="Model">
        <KeyValueRow label="Provider" value={formatProvider(modelReference.provider)} />
        <KeyValueRow label="Model" value={modelReference.model} />
      </InspectorCard>

      <InspectorCard title="Tools">
        <ToolList
          tools={tools}
          onOpenTools={onOpenTools}
          onOpenTool={onOpenTool}
        />
      </InspectorCard>

      <InspectorCard title="Context Space">
        <ContextSpaceSummary contextSpaces={contextSpaces} />
      </InspectorCard>

      <InspectorCard title="Multi-Agent Teams">
        <div className="flex items-center gap-2 text-sm font-medium text-zinc-800 dark:text-zinc-200">
          <Users className="size-3.5 shrink-0 text-zinc-500 dark:text-zinc-500" />
          <span className="truncate">No team members</span>
        </div>
      </InspectorCard>

      <InspectorCard title="Memory">
        <div className="truncate text-sm font-medium text-zinc-800 dark:text-zinc-200">
          Off
        </div>
      </InspectorCard>
    </div>
  );
}

function ToolList({
  tools,
  onOpenTools,
  onOpenTool,
}: {
  tools: AgentToolMetadata[];
  onOpenTools: () => void;
  onOpenTool: (toolName: string) => void;
}) {
  if (tools.length === 0) {
    return (
      <span className="text-sm font-medium text-zinc-800 dark:text-zinc-200">
        No tools attached
      </span>
    );
  }

  const visibleTools = tools.slice(0, 2);
  const hiddenToolCount = tools.length - visibleTools.length;

  return (
    <div className="flex flex-wrap gap-1.5">
      {visibleTools.map((tool) => (
        <button
          key={tool.name}
          type="button"
          title={formatToolTitle(tool)}
          onClick={() => onOpenTool(tool.name)}
          className="inline-flex items-center gap-1.5 rounded-full border border-zinc-200 bg-white px-2 py-1 text-xs font-medium text-zinc-700 transition hover:border-zinc-300 hover:bg-zinc-50 hover:text-zinc-950 dark:border-zinc-700 dark:bg-zinc-900 dark:text-zinc-200 dark:hover:border-zinc-600 dark:hover:bg-zinc-800 dark:hover:text-zinc-50"
        >
          <Wrench className="size-3 text-zinc-500 dark:text-zinc-400" />
          {getToolDisplayName(tool)}
        </button>
      ))}

      {hiddenToolCount > 0 && (
        <button
          type="button"
          onClick={onOpenTools}
          className="inline-flex items-center rounded-full border border-zinc-200 bg-white px-2 py-1 text-xs font-medium text-zinc-500 transition hover:border-zinc-300 hover:bg-zinc-50 hover:text-zinc-950 dark:border-zinc-700 dark:bg-zinc-900 dark:text-zinc-400 dark:hover:border-zinc-600 dark:hover:bg-zinc-800 dark:hover:text-zinc-100"
        >
          +{hiddenToolCount} more
        </button>
      )}
    </div>
  );
}

function ContextSpaceSummary({
  contextSpaces,
}: {
  contextSpaces: AgentContextSpaceMetadata[];
}) {
  if (contextSpaces.length === 0) {
    return (
      <span className="text-sm font-medium text-zinc-800 dark:text-zinc-200">
        No context space attached
      </span>
    );
  }

  const visibleContextSpace = contextSpaces[0];
  const hiddenContextSpaceCount = contextSpaces.length - 1;

  return (
    <div className="space-y-1.5">
      <button
        type="button"
        title={
          visibleContextSpace.description ||
          visibleContextSpace.name ||
          visibleContextSpace.id
        }
        onClick={() => navigateToContextSpace(visibleContextSpace.id)}
        className="group -mx-1.5 flex w-[calc(100%+0.75rem)] cursor-pointer flex-col rounded-md border border-transparent px-1.5 py-1 text-left transition hover:border-zinc-200 hover:bg-white focus-visible:border-zinc-400 focus-visible:bg-white focus-visible:outline-none dark:hover:border-zinc-700 dark:hover:bg-zinc-900 dark:focus-visible:border-zinc-600 dark:focus-visible:bg-zinc-900"
      >
        <span className="flex w-full min-w-0 items-center gap-2 text-sm font-semibold text-zinc-900 transition group-hover:text-zinc-950 dark:text-zinc-100 dark:group-hover:text-white">
          <Database className="size-3.5 shrink-0 text-zinc-500 transition group-hover:text-zinc-700 dark:text-zinc-500 dark:group-hover:text-zinc-300" />
          <span className="truncate">
            {visibleContextSpace.name || visibleContextSpace.id}
          </span>
        </span>

        <span className="mt-1 truncate text-xs text-zinc-500 dark:text-zinc-500">
          {formatContextSpaceCounts(visibleContextSpace)}
          {hiddenContextSpaceCount > 0
            ? ` · +${hiddenContextSpaceCount} more`
            : ''}
        </span>
      </button>
    </div>
  );
}

function navigateToContextSpace(contextSpaceId: string) {
  const basePath = getDashboardBasePath().replace(/\/+$/g, '');

  window.history.pushState(
    {},
    '',
    `${basePath}/context-spaces/${encodeURIComponent(contextSpaceId)}`,
  );

  window.dispatchEvent(new PopStateEvent('popstate'));
}

function formatToolTitle(tool: AgentToolMetadata): string {
  const parts = [
    tool.description?.trim() || 'No description provided.',
    tool.inputType ? `Input: ${tool.inputType}` : undefined,
    tool.outputType ? `Output: ${tool.outputType}` : undefined,
  ].filter(Boolean);

  return parts.join('\n');
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
      <div className="text-xs font-semibold uppercase tracking-[0.14em] text-zinc-500 dark:text-zinc-500">
        {title}
      </div>

      <div className="mt-2 space-y-1.5 text-sm">{children}</div>
    </section>
  );
}

function KeyValueRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-center justify-between gap-4">
      <span className="text-zinc-500 dark:text-zinc-500">{label}</span>
      <span className="min-w-0 truncate text-right font-medium text-zinc-800 dark:text-zinc-200">
        {value}
      </span>
    </div>
  );
}

function formatContextSpaceCounts(
  contextSpace: AgentContextSpaceMetadata,
): string {
  const counts = [
    formatCount(contextSpace.skillCount, 'Skill'),
    formatCount(contextSpace.documentCount, 'Document'),
  ].filter(Boolean);

  return counts.length > 0 ? counts.join(' · ') : 'Document summary unavailable';
}

function formatCount(value: number | undefined, label: string): string | null {
  if (typeof value !== 'number') {
    return null;
  }

  return `${value} ${label}${value === 1 ? '' : 's'}`;
}

function formatProvider(provider: string): string {
  const normalizedProvider = provider.trim().toLowerCase();

  if (normalizedProvider === 'openai') {
    return 'OpenAI';
  }

  if (normalizedProvider === 'azure-openai') {
    return 'Azure OpenAI';
  }

  if (normalizedProvider === 'ollama') {
    return 'Ollama';
  }

  return provider
    .replace(/[-_]+/g, ' ')
    .trim()
    .split(/\s+/)
    .filter(Boolean)
    .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
    .join(' ');
}

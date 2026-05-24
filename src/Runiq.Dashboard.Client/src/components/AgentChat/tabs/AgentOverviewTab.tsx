import type { ReactNode } from 'react';
import { Database, Wrench } from 'lucide-react';

import type {
  AgentContextSpaceMetadata,
  AgentMetadata,
  AgentToolMetadata,
} from '../../../api/agentMetadataApi';
import { getDashboardBasePath } from '../../../dashboardConfig';
import { parseModelReference } from '../../../utils/modelReference';

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
    <div className="flex min-h-0 flex-col gap-3">
      <InspectorCard title="Capabilities">
        <OverviewSection label="Tools">
          <ToolList
            tools={tools}
            onOpenTools={onOpenTools}
            onOpenTool={onOpenTool}
          />
        </OverviewSection>

        <OverviewSection label="Context Spaces">
          <ContextSpaceList contextSpaces={contextSpaces} />
        </OverviewSection>

        <OverviewSection label="Workflows">
          <span className="text-sm font-medium text-zinc-800 dark:text-zinc-200">
            No workflows attached
          </span>
        </OverviewSection>
      </InspectorCard>

      <InspectorCard title="Model">
        <OverviewRow label="Provider">
          <span className="truncate text-right font-medium text-zinc-800 dark:text-zinc-200">
            {modelReference.provider}
          </span>
        </OverviewRow>

        <OverviewRow label="Model">
          <span className="truncate text-right font-medium text-zinc-800 dark:text-zinc-200">
            {modelReference.model}
          </span>
        </OverviewRow>
      </InspectorCard>

      <InspectorCard title="System Prompt" className="min-h-0 flex-1">
        <div className="max-h-48 overflow-y-auto whitespace-pre-wrap break-words pr-2 text-sm leading-6 text-zinc-700 [scrollbar-width:thin] [scrollbar-color:rgb(161_161_170)_transparent] dark:text-zinc-300 dark:[scrollbar-color:rgb(82_82_91)_transparent] [&::-webkit-scrollbar]:w-1.5 [&::-webkit-scrollbar-track]:bg-transparent [&::-webkit-scrollbar-thumb]:rounded-full [&::-webkit-scrollbar-thumb]:bg-zinc-300 dark:[&::-webkit-scrollbar-thumb]:bg-zinc-700">
          {formatSystemPrompt(agent.instructions)}
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
          {tool.name}
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

function ContextSpaceList({
  contextSpaces,
}: {
  contextSpaces: AgentContextSpaceMetadata[];
}) {
  if (contextSpaces.length === 0) {
    return (
      <span className="text-sm font-medium text-zinc-800 dark:text-zinc-200">
        No context spaces attached
      </span>
    );
  }

  return (
    <div className="flex flex-wrap gap-1.5">
      {contextSpaces.map((contextSpace) => (
        <button
          key={contextSpace.id}
          type="button"
          title={contextSpace.description || contextSpace.name || contextSpace.id}
          onClick={() => navigateToContextSpace(contextSpace.id)}
          className="inline-flex items-center gap-1.5 rounded-full border border-zinc-200 bg-white px-2 py-1 text-xs font-medium text-zinc-700 transition hover:border-zinc-300 hover:bg-zinc-50 hover:text-zinc-950 dark:border-zinc-700 dark:bg-zinc-900 dark:text-zinc-200 dark:hover:border-zinc-600 dark:hover:bg-zinc-800 dark:hover:text-zinc-50"
        >
          <Database className="size-3 text-zinc-500 dark:text-zinc-400" />
          {contextSpace.name || contextSpace.id}
        </button>
      ))}
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

function formatSystemPrompt(value: string | undefined): string {
  const trimmedValue = value?.trim();

  if (!trimmedValue) {
    return 'No system prompt configured.';
  }

  return trimmedValue;
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

      <div className="mt-3 space-y-2.5 text-sm">{children}</div>
    </section>
  );
}

function OverviewSection({
  label,
  children,
}: {
  label: string;
  children: ReactNode;
}) {
  return (
    <div className="space-y-1.5">
      <div className="text-zinc-500 dark:text-zinc-500">{label}</div>
      <div className="min-w-0">{children}</div>
    </div>
  );
}

function OverviewRow({
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
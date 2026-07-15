import type { ReactNode } from 'react';
import { Wrench } from 'lucide-react';

import type { AgentMetadata, AgentToolMetadata } from '../../../api/agentMetadataApi';
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

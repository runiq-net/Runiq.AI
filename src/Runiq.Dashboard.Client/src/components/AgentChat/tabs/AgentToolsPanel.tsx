import { ArrowLeft, ChevronRight, Wrench } from 'lucide-react';

import type { AgentToolMetadata } from '../../../api/agentMetadataApi';
import { getToolDisplayName } from './agentToolDisplay';

type AgentToolsPanelProps = {
  tools: AgentToolMetadata[];
  onBack: () => void;
  onOpenTool: (toolName: string) => void;
};

export function AgentToolsPanel({
  tools,
  onBack,
  onOpenTool,
}: AgentToolsPanelProps) {
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

      <div className="mb-3">
        <div className="text-sm font-semibold text-zinc-950 dark:text-zinc-100">
          Tools
        </div>
        <div className="mt-1 text-xs text-zinc-500 dark:text-zinc-500">
          {tools.length} attached tool{tools.length === 1 ? '' : 's'}
        </div>
      </div>

      <div className="min-h-0 space-y-2 overflow-y-auto pr-1 [scrollbar-width:thin] [scrollbar-color:rgb(161_161_170)_transparent] dark:[scrollbar-color:rgb(82_82_91)_transparent] [&::-webkit-scrollbar]:w-1.5 [&::-webkit-scrollbar-track]:bg-transparent [&::-webkit-scrollbar-thumb]:rounded-full [&::-webkit-scrollbar-thumb]:bg-zinc-300 dark:[&::-webkit-scrollbar-thumb]:bg-zinc-700">
        {tools.map((tool) => (
          <button
            key={tool.name}
            type="button"
            onClick={() => onOpenTool(tool.name)}
            className="group w-full rounded-md border border-zinc-200 bg-zinc-50 p-3 text-left transition hover:border-zinc-300 hover:bg-white dark:border-zinc-800 dark:bg-zinc-900/40 dark:hover:border-zinc-700 dark:hover:bg-zinc-900"
          >
            <div className="flex items-start gap-2">
              <div className="mt-0.5 rounded-md border border-zinc-200 bg-white p-1.5 text-zinc-500 dark:border-zinc-700 dark:bg-zinc-950 dark:text-zinc-400">
                <Wrench className="size-3.5" />
              </div>

              <div className="min-w-0 flex-1">
                <div className="flex items-center justify-between gap-2">
                  <div className="truncate text-sm font-semibold text-zinc-900 dark:text-zinc-100">
                    {getToolDisplayName(tool)}
                  </div>

                  <ChevronRight className="size-4 shrink-0 text-zinc-400 transition group-hover:text-zinc-700 dark:text-zinc-600 dark:group-hover:text-zinc-300" />
                </div>

                <div className="mt-1 line-clamp-2 text-xs leading-5 text-zinc-500 dark:text-zinc-400">
                  {formatDescription(tool.description)}
                </div>

                <div className="mt-2 text-xs text-zinc-500 dark:text-zinc-500">
                  Input {tool.inputType || 'Unknown'} · Output{' '}
                  {tool.outputType || 'Unknown'}
                </div>
              </div>
            </div>
          </button>
        ))}
      </div>
    </div>
  );
}

function formatDescription(description: string | undefined): string {
  const trimmedDescription = description?.trim();

  if (!trimmedDescription) {
    return 'No description provided.';
  }

  return trimmedDescription;
}

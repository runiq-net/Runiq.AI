import type { AgentMetadata } from '../../../api/agentMetadataApi';

type AgentPromptTabProps = {
  agent: AgentMetadata;
};

export function AgentPromptTab({ agent }: AgentPromptTabProps) {
  return (
    <div className="flex h-full min-h-0 flex-col">
      <section className="flex min-h-0 flex-1 flex-col rounded-md border border-zinc-200 bg-zinc-50 p-3 dark:border-zinc-800 dark:bg-zinc-900/40">
        <div className="shrink-0 text-xs font-semibold uppercase tracking-[0.14em] text-zinc-500 dark:text-zinc-500">
          System Prompt
        </div>

        <div className="mt-3 min-h-0 flex-1 overflow-y-auto whitespace-pre-wrap break-words pr-2 text-sm leading-6 text-zinc-700 [scrollbar-width:thin] [scrollbar-color:rgb(161_161_170)_transparent] dark:text-zinc-300 dark:[scrollbar-color:rgb(82_82_91)_transparent] [&::-webkit-scrollbar]:w-1.5 [&::-webkit-scrollbar-track]:bg-transparent [&::-webkit-scrollbar-thumb]:rounded-full [&::-webkit-scrollbar-thumb]:bg-zinc-300 dark:[&::-webkit-scrollbar-thumb]:bg-zinc-700">
          {formatSystemPrompt(agent.instructions)}
        </div>
      </section>
    </div>
  );
}

function formatSystemPrompt(value: string | undefined): string {
  const trimmedValue = value?.trim();

  if (!trimmedValue) {
    return 'No system prompt configured.';
  }

  return trimmedValue;
}

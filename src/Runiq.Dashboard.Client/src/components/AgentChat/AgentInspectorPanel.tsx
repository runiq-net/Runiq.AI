import { useState } from 'react';

import type { AgentMetadata } from '../../api/agentMetadataApi';
import { AgentOverviewTab } from './tabs/AgentOverviewTab';
import { AgentPromptTab } from './tabs/AgentPromptTab';
import { RunningBehaviorTab } from './tabs/RunningBehaviorTab';
import type { AgentChatMethod } from '../../types/agentChat';
import { AgentToolsPanel } from './tabs/AgentToolsPanel';
import { AgentToolDetailPanel } from './tabs/AgentToolDetailPanel';

type AgentInspectorPanelProps = {
  agent: AgentMetadata;
  chatMethod: AgentChatMethod;
  onChatMethodChange: (chatMethod: AgentChatMethod) => void;
};

type InspectorTab = 'overview' | 'running-behavior' | 'prompt';

type InspectorView =
  | { kind: 'tab' }
  | { kind: 'tools' }
  | { kind: 'tool-detail'; toolName: string; returnTo: 'overview' | 'tools' };

const tabs: Array<{
  key: InspectorTab;
  label: string;
}> = [
  { key: 'overview', label: 'Overview' },
  { key: 'running-behavior', label: 'Behavior' },
  { key: 'prompt', label: 'Prompt' },
];

export function AgentInspectorPanel({
  agent,
  chatMethod,
  onChatMethodChange,
}: AgentInspectorPanelProps) {
  const [activeTab, setActiveTab] = useState<InspectorTab>('overview');
  const [inspectorView, setInspectorView] = useState<InspectorView>({
    kind: 'tab',
  });

  const tools = agent.tools ?? [];

  const selectedTool =
    inspectorView.kind === 'tool-detail'
      ? tools.find((tool) => tool.name === inspectorView.toolName)
      : undefined;

  function handleTabChange(tab: InspectorTab) {
    setActiveTab(tab);
    setInspectorView({ kind: 'tab' });
  }

  function handleOpenTools() {
    setInspectorView({ kind: 'tools' });
  }

  function handleOpenToolFromOverview(toolName: string) {
    setInspectorView({
      kind: 'tool-detail',
      toolName,
      returnTo: 'overview',
    });
  }

  function handleOpenToolFromTools(toolName: string) {
    setInspectorView({
      kind: 'tool-detail',
      toolName,
      returnTo: 'tools',
    });
  }

  function handleBackFromToolDetail() {
    if (inspectorView.kind !== 'tool-detail') {
      setInspectorView({ kind: 'tab' });
      return;
    }

    setInspectorView(
      inspectorView.returnTo === 'tools' ? { kind: 'tools' } : { kind: 'tab' },
    );
  }

  return (
    <aside className="hidden w-[320px] shrink-0 rounded-lg border border-zinc-200 bg-white shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40 dark:shadow-none xl:flex xl:min-h-0 xl:flex-col">
      <div className="border-b border-zinc-200 px-3 py-3 dark:border-zinc-800">
        <div className="truncate text-sm font-semibold text-zinc-950 dark:text-zinc-100">
          {agent.name || agent.id}
        </div>

        <div className="mt-2 grid grid-cols-3 rounded-md bg-zinc-100 p-1 text-xs font-medium dark:bg-zinc-900">
          {tabs.map((tab) => (
            <button
              key={tab.key}
              type="button"
              onClick={() => handleTabChange(tab.key)}
              className={[
                'rounded px-1.5 py-1 transition',
                inspectorView.kind === 'tab' && activeTab === tab.key
                  ? 'bg-white text-zinc-950 shadow-sm dark:bg-zinc-800 dark:text-zinc-100'
                  : 'text-zinc-500 hover:text-zinc-950 dark:text-zinc-500 dark:hover:text-zinc-200',
              ].join(' ')}
            >
              {tab.label}
            </button>
          ))}
        </div>
      </div>

      <div className="min-h-0 flex-1 overflow-hidden p-3">
        {inspectorView.kind === 'tools' && (
          <AgentToolsPanel
            tools={tools}
            onBack={() => setInspectorView({ kind: 'tab' })}
            onOpenTool={handleOpenToolFromTools}
          />
        )}

        {inspectorView.kind === 'tool-detail' && selectedTool && (
          <AgentToolDetailPanel
            tool={selectedTool}
            onBack={handleBackFromToolDetail}
          />
        )}

        {inspectorView.kind === 'tool-detail' && !selectedTool && (
          <AgentToolsPanel
            tools={tools}
            onBack={() => setInspectorView({ kind: 'tab' })}
            onOpenTool={handleOpenToolFromTools}
          />
        )}

        {inspectorView.kind === 'tab' && activeTab === 'overview' && (
          <AgentOverviewTab
            agent={agent}
            onOpenTools={handleOpenTools}
            onOpenTool={handleOpenToolFromOverview}
          />
        )}

        {inspectorView.kind === 'tab' && activeTab === 'running-behavior' && (
          <RunningBehaviorTab
            agent={agent}
            chatMethod={chatMethod}
            onChatMethodChange={onChatMethodChange}
          />
        )}

        {inspectorView.kind === 'tab' && activeTab === 'prompt' && (
          <AgentPromptTab agent={agent} />
        )}
      </div>
    </aside>
  );
}

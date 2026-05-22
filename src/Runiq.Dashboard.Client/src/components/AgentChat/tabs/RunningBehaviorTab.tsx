import type { AgentMetadata } from '../../../api/agentMetadataApi';
import type { AgentChatMethod } from '../../../types/agentChat';

type RunningBehaviorTabProps = {
  agent: AgentMetadata;
  chatMethod: AgentChatMethod;
  onChatMethodChange: (chatMethod: AgentChatMethod) => void;
};

export function RunningBehaviorTab({
  agent,
  chatMethod,
  onChatMethodChange,
}: RunningBehaviorTabProps) {
  return (
    <div className="space-y-3">
      <InspectorCard title="Chat Method">
        <ChatMethodSelector
          value={chatMethod}
          onChange={onChatMethodChange}
        />
      </InspectorCard>

      <InspectorCard title="Model Behavior">
        <OverviewRow
          label="Reasoning"
          value={formatValue(agent.reasoningEffort)}
        />

        <OverviewRow label="Verbosity" value={formatValue(agent.verbosity)} />
      </InspectorCard>
    </div>
  );
}

function ChatMethodSelector({
  value,
  onChange,
}: {
  value: AgentChatMethod;
  onChange: (value: AgentChatMethod) => void;
}) {
  return (
    <div className="space-y-2">
      <ChatMethodRadio
        label="Stream"
        value="stream"
        checked={value === 'stream'}
        onChange={onChange}
      />

      <ChatMethodRadio
        label="Result"
        value="result"
        checked={value === 'result'}
        onChange={onChange}
      />
    </div>
  );
}

function ChatMethodRadio({
  label,
  value,
  checked,
  onChange,
}: {
  label: string;
  value: AgentChatMethod;
  checked: boolean;
  onChange: (value: AgentChatMethod) => void;
}) {
  return (
    <label className="flex cursor-pointer items-center gap-2 text-sm font-medium">
      <input
        type="radio"
        name="agent-chat-method"
        value={value}
        checked={checked}
        onChange={() => onChange(value)}
        className="size-3.5 accent-[var(--runiq-control-accent)]"
      />

      <span
        className={
          checked
            ? 'text-zinc-950 dark:text-zinc-100'
            : 'text-zinc-600 dark:text-zinc-400'
        }
      >
        {label}
      </span>
    </label>
  );
}

function formatValue(value: string | undefined): string {
  return value && value.trim().length > 0 ? value : 'Default';
}

function InspectorCard({
  title,
  children,
}: {
  title: string;
  children: React.ReactNode;
}) {
  return (
    <section className="rounded-md border border-zinc-200 bg-zinc-50 p-3 dark:border-zinc-800 dark:bg-zinc-900/40">
      <div className="text-xs font-semibold uppercase tracking-[0.14em] text-zinc-500 dark:text-zinc-500">
        {title}
      </div>

      <div className="mt-3 space-y-3 text-sm">{children}</div>
    </section>
  );
}

function OverviewRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-center justify-between gap-4">
      <span className="text-zinc-500 dark:text-zinc-500">{label}</span>

      <span className="truncate text-right font-medium text-zinc-800 dark:text-zinc-200">
        {value}
      </span>
    </div>
  );
}
import { Cable, CheckCircle2, KeyRound, Link2, ShieldCheck, Wrench } from 'lucide-react';
import type { ReactNode } from 'react';

import type { McpInfo } from '../../api/mcpApi';

type McpServerDetailsPanelProps = {
  info: McpInfo;
};

export function McpServerDetailsPanel({ info }: McpServerDetailsPanelProps) {
  const mode = info.stateless ? 'Stateless' : 'Stateful';
  const status = info.enabled ? 'Enabled' : 'Not detected';

  return (
    <aside className="min-w-0 rounded-xl border border-zinc-200 bg-white p-4 shadow-sm dark:border-zinc-800 dark:bg-zinc-950">
      <div className="space-y-2">
        <InspectorCard title="Status">
          <StatusCard enabled={info.enabled} label={status} />
        </InspectorCard>

        <InspectorCard title="Connection">
          <div className="flex min-w-0 items-start gap-2">
            <Link2 className="mt-0.5 size-3.5 shrink-0 text-zinc-500 dark:text-zinc-500" />
            <span className="break-all font-mono text-sm font-medium text-zinc-900 dark:text-zinc-100">
              {info.fullUrl ?? 'Not available'}
            </span>
          </div>
        </InspectorCard>

        <InspectorCard title="Transport">
          <KeyValueRow
            icon={<Cable className="size-3.5" aria-hidden="true" />}
            label="Type"
            value={info.transport}
          />
          <KeyValueRow
            icon={<ShieldCheck className="size-3.5" aria-hidden="true" />}
            label="Mode"
            value={mode}
          />
        </InspectorCard>

        <InspectorCard title="Access">
          <KeyValueRow
            icon={<KeyRound className="size-3.5" aria-hidden="true" />}
            label="Authentication"
            value={info.authentication}
          />
        </InspectorCard>

        <InspectorCard title="Tools">
          <KeyValueRow
            icon={<Wrench className="size-3.5" aria-hidden="true" />}
            label="Exposed"
            value={String(info.tools.length)}
          />
        </InspectorCard>
      </div>
    </aside>
  );
}

type StatusBadgeProps = {
  enabled: boolean;
  label: string;
};

function StatusCard({ enabled, label }: StatusBadgeProps) {
  return (
    <div
      className={[
        'flex items-center justify-between gap-3 rounded-md px-2.5 py-2 text-sm font-semibold',
        enabled
          ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950/50 dark:text-emerald-300'
          : 'bg-zinc-100 text-zinc-600 dark:bg-zinc-900 dark:text-zinc-400',
      ].join(' ')}
    >
      <span>{label}</span>
      {enabled ? (
        <CheckCircle2 className="size-4" aria-hidden="true" />
      ) : (
        <span className="size-2.5 rounded-full bg-zinc-400" aria-hidden="true" />
      )}
    </div>
  );
}

function InspectorCard({
  title,
  children,
}: {
  title: string;
  children: ReactNode;
}) {
  return (
    <section className="rounded-md border border-zinc-200 bg-zinc-50 p-3 dark:border-zinc-800 dark:bg-zinc-900/40">
      <div className="text-xs font-semibold uppercase tracking-[0.14em] text-zinc-500 dark:text-zinc-500">
        {title}
      </div>

      <div className="mt-2 space-y-1.5 text-sm">{children}</div>
    </section>
  );
}

function KeyValueRow({
  icon,
  label,
  value,
}: {
  icon: ReactNode;
  label: string;
  value: string;
}) {
  return (
    <div className="flex items-center justify-between gap-4">
      <span className="flex min-w-0 items-center gap-1.5 text-zinc-500 dark:text-zinc-500">
        <span className="shrink-0 text-zinc-500 dark:text-zinc-500">{icon}</span>
        <span className="truncate">{label}</span>
      </span>
      <span className="min-w-0 truncate text-right font-medium text-zinc-800 dark:text-zinc-200">
        {value}
      </span>
    </div>
  );
}

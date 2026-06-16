import { Server, Wrench } from 'lucide-react';
import type { ReactNode } from 'react';

import type { McpInfo, McpToolInfo } from '../../api/mcpApi';

type McpToolInspectorPanelProps = {
  info: McpInfo;
  tool: McpToolInfo;
};

export function McpToolInspectorPanel({ info, tool }: McpToolInspectorPanelProps) {
  return (
    <aside className="hidden w-[320px] shrink-0 rounded-lg border border-zinc-200 bg-white shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40 dark:shadow-none xl:flex xl:min-h-0 xl:flex-col">
      <div className="border-b border-zinc-200 px-3 py-3 dark:border-zinc-800">
        <div className="truncate text-sm font-semibold text-zinc-950 dark:text-zinc-100">
          {formatDisplayName(tool.name)}
        </div>
      </div>

      <div className="min-h-0 flex-1 overflow-hidden p-3">
        <div className="flex min-h-0 flex-col gap-3">
          <InspectorCard title="Description">
            <p className="text-sm leading-6 text-zinc-600 dark:text-zinc-400">
              {tool.description || 'No description.'}
            </p>
          </InspectorCard>

          <InspectorCard title="Connection">
            <InspectorRow label="Endpoint">
              <span className="truncate font-mono text-sm font-medium text-zinc-800 dark:text-zinc-200">
                {info.endpoint ?? 'Not detected'}
              </span>
            </InspectorRow>
          </InspectorCard>

          <InspectorCard title="Runtime">
            <div className="space-y-3 text-sm">
              <InspectorRow label="Source">
                <span className="inline-flex items-center gap-1.5 rounded-full border border-zinc-200 bg-white px-2 py-1 text-xs font-medium text-zinc-700 dark:border-zinc-700 dark:bg-zinc-950 dark:text-zinc-300">
                  <Wrench className="size-3" />
                  {tool.source}
                </span>
              </InspectorRow>

              <InspectorRow label="Transport">
                <span className="truncate text-right font-medium text-zinc-800 dark:text-zinc-200">
                  {info.transport}
                </span>
              </InspectorRow>

              <InspectorRow label="Mode">
                <span className="truncate text-right font-medium text-zinc-800 dark:text-zinc-200">
                  {info.stateless ? 'Stateless' : 'Stateful'}
                </span>
              </InspectorRow>
            </div>
          </InspectorCard>

          <InspectorCard title="Input">
            <InspectorRow label="Fields">
              <span className="font-medium text-zinc-800 dark:text-zinc-200">
                {getInputFieldCount(tool)}
              </span>
            </InspectorRow>
          </InspectorCard>
        </div>
      </div>
    </aside>
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
      <div className="mb-3 text-xs font-semibold uppercase tracking-[0.14em] text-zinc-500 dark:text-zinc-500">
        {title}
      </div>

      {children}
    </section>
  );
}

function InspectorRow({
  label,
  children,
}: {
  label: string;
  children: ReactNode;
}) {
  return (
    <div className="flex items-center justify-between gap-4">
      <span className="inline-flex items-center gap-2 text-zinc-500 dark:text-zinc-500">
        {label === 'Endpoint' && <Server className="size-4" />}
        {label}
      </span>
      <div className="min-w-0 text-right">{children}</div>
    </div>
  );
}

function getInputFieldCount(tool: McpToolInfo): number {
  return Object.keys(tool.inputSchema?.properties ?? {}).length;
}

function formatDisplayName(value: string): string {
  return value
    .replace(/[-_]+/g, ' ')
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    .trim()
    .split(/\s+/)
    .filter(Boolean)
    .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
    .join(' ');
}

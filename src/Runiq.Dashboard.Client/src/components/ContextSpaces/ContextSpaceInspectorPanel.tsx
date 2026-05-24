import { Bot } from 'lucide-react';
import type { ReactNode } from 'react';

import type { ContextSpaceMetadata } from '../../api/agentMetadataApi';
import { getDashboardBasePath } from '../../dashboardConfig';

type ContextSpaceInspectorPanelProps = {
  contextSpace: ContextSpaceMetadata;
  documentCount?: number;
};

export function ContextSpaceInspectorPanel({
  contextSpace,
  documentCount,
}: ContextSpaceInspectorPanelProps) {
  return (
    <aside className="hidden w-[320px] shrink-0 rounded-lg border border-zinc-200 bg-white shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40 dark:shadow-none xl:flex xl:min-h-0 xl:flex-col">
      <div className="border-b border-zinc-200 px-4 py-4 dark:border-zinc-800">
        <div className="truncate text-sm font-semibold text-zinc-950 dark:text-zinc-100">
          {contextSpace.name || contextSpace.id}
        </div>

        <div className="mt-1 truncate font-mono text-xs text-zinc-500 dark:text-zinc-500">
          {contextSpace.id}
        </div>
      </div>

      <div className="min-h-0 flex-1 space-y-3 overflow-y-auto p-3 [scrollbar-width:thin] [scrollbar-color:rgb(161_161_170)_transparent] dark:[scrollbar-color:rgb(82_82_91)_transparent] [&::-webkit-scrollbar]:w-1.5 [&::-webkit-scrollbar-track]:bg-transparent [&::-webkit-scrollbar-thumb]:rounded-full [&::-webkit-scrollbar-thumb]:bg-zinc-300 dark:[&::-webkit-scrollbar-thumb]:bg-zinc-700">
        <InspectorCard title="Summary">
          <div className="space-y-3 text-sm">
            <InspectorRow label="Skills">
              <span className="font-medium text-zinc-800 dark:text-zinc-200">
                {contextSpace.skills.length}
              </span>
            </InspectorRow>
            <InspectorRow label="Source groups">
              <span className="font-medium text-zinc-800 dark:text-zinc-200">
                {contextSpace.sources.length}
              </span>
            </InspectorRow>
            <InspectorRow label="Documents">
              <span className="font-medium text-zinc-800 dark:text-zinc-200">
                {documentCount ?? '...'}
              </span>
            </InspectorRow>
            <InspectorRow label="Agents">
              <span className="font-medium text-zinc-800 dark:text-zinc-200">
                {contextSpace.attachedAgents.length}
              </span>
            </InspectorRow>
          </div>
        </InspectorCard>

        <ContextSpaceAttachedAgentsCard contextSpace={contextSpace} />

        <InspectorCard title="Technical Details">
          <div className="space-y-3">
            <InspectorRow label="Skill sources">
              <span className="font-medium text-zinc-800 dark:text-zinc-200">
                {contextSpace.skillSources.length}
              </span>
            </InspectorRow>

            {contextSpace.skillSources.length === 0 ? (
              <p className="text-sm text-zinc-600 dark:text-zinc-400">
                No skill sources configured.
              </p>
            ) : (
              <div className="space-y-2">
                {contextSpace.skillSources.map((skillSource) => (
                  <div
                    key={skillSource.id}
                    className="rounded-md border border-zinc-200 bg-white p-3 dark:border-zinc-800 dark:bg-zinc-950/50"
                  >
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="min-w-0 truncate text-sm font-medium text-zinc-950 dark:text-zinc-100">
                        {skillSource.name || skillSource.id}
                      </span>
                      <span className="rounded-md border border-zinc-200 bg-zinc-50 px-2 py-0.5 text-xs text-zinc-600 dark:border-zinc-800 dark:bg-zinc-900 dark:text-zinc-400">
                        {formatSourceKind(skillSource.kind)}
                      </span>
                    </div>

                    <div className="mt-2 break-all font-mono text-xs text-zinc-500 dark:text-zinc-500">
                      {formatSkillSourceLocation(skillSource)}
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        </InspectorCard>
      </div>
    </aside>
  );
}

function ContextSpaceAttachedAgentsCard({
  contextSpace,
}: {
  contextSpace: ContextSpaceMetadata;
}) {
  return (
    <InspectorCard title="Attached Agents">
      {contextSpace.attachedAgents.length === 0 ? (
        <span className="text-sm text-zinc-600 dark:text-zinc-400">
          No agents attached.
        </span>
      ) : (
        <div className="space-y-2">
          {contextSpace.attachedAgents.map((agent) => (
            <button
              key={agent.id}
              type="button"
              onClick={() => navigateToAgent(agent.id)}
              className="flex w-full items-center gap-3 rounded-md border border-zinc-200 bg-white px-3 py-2 text-left transition hover:border-zinc-300 hover:bg-zinc-100 dark:border-zinc-800 dark:bg-zinc-950/50 dark:hover:border-zinc-700 dark:hover:bg-zinc-900"
            >
              <span className="inline-flex size-7 shrink-0 items-center justify-center rounded-lg border border-zinc-200 bg-zinc-50 text-zinc-600 dark:border-zinc-800 dark:bg-zinc-950 dark:text-zinc-300">
                <Bot className="size-3.5" />
              </span>

              <div className="min-w-0">
                <div className="truncate text-sm font-medium text-zinc-950 dark:text-zinc-100">
                  {agent.name}
                </div>

                <div className="truncate font-mono text-xs text-zinc-500 dark:text-zinc-500">
                  {agent.id}
                </div>
              </div>
            </button>
          ))}
        </div>
      )}
    </InspectorCard>
  );
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
      <span className="text-zinc-500 dark:text-zinc-500">{label}</span>
      <div className="min-w-0 text-right">{children}</div>
    </div>
  );
}

function navigateToAgent(agentId: string) {
  const basePath = getDashboardBasePath().replace(/\/+$/g, '');

  window.history.pushState(
    {},
    '',
    `${basePath}/agents/${encodeURIComponent(agentId)}/chat/new`,
  );

  window.dispatchEvent(new PopStateEvent('popstate'));
}

function formatSkillSourceLocation(
  skillSource: ContextSpaceMetadata['skillSources'][number],
): string {
  if (skillSource.kind === 'S3') {
    return `s3://${skillSource.bucketName ?? ''}/${skillSource.prefix ?? ''}`;
  }

  return skillSource.path || skillSource.id;
}

function formatSourceKind(value: string): string {
  return value
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    .replace(/[-_]+/g, ' ')
    .trim();
}

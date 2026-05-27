import type { ReactNode } from 'react';

import { WorkflowStudioTopbar } from '../components/workflows/WorkflowStudioTopbar';

type WorkflowStudioLayoutProps = {
  workflowName: string;
  status: string;
  children: ReactNode;
};

export function WorkflowStudioLayout({
  workflowName,
  status,
  children,
}: WorkflowStudioLayoutProps) {
  return (
    <main className="flex h-dvh w-full flex-col overflow-hidden bg-zinc-50 text-zinc-950 dark:bg-[#050505] dark:text-zinc-100">
      <WorkflowStudioTopbar
        workflowName={workflowName}
        status={status}
      />

      <section className="min-h-0 flex-1 overflow-hidden">{children}</section>
    </main>
  );
}

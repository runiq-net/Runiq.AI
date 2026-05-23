import { useEffect, useMemo, useState } from 'react';

import { getTools, type ToolMetadata } from '../api/agentMetadataApi';
import { ToolInspectorPanel } from '../components/ToolPlayground/ToolInspectorPanel';
import { ToolRunPanel } from '../components/ToolPlayground/ToolRunPanel';
import { getDashboardBasePath } from '../dashboardConfig';

type ToolDetailPageProps = {
  toolName: string;
};

export function ToolDetailPage({ toolName }: ToolDetailPageProps) {
  const [tools, setTools] = useState<ToolMetadata[]>([]);
  const [isLoading, setLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  useEffect(() => {
    let isMounted = true;

    async function loadTools() {
      try {
        setLoading(true);
        setErrorMessage(null);

        const result = await getTools(getDashboardBasePath());

        if (!isMounted) {
          return;
        }

        setTools(result);
      } catch (error) {
        if (!isMounted) {
          return;
        }

        setErrorMessage(
          error instanceof Error ? error.message : 'Tool metadata could not be loaded.',
        );
      } finally {
        if (isMounted) {
          setLoading(false);
        }
      }
    }

    void loadTools();

    return () => {
      isMounted = false;
    };
  }, []);

  const tool = useMemo(() => {
    return tools.find(
      (item) => item.name.toLowerCase() === toolName.toLowerCase(),
    );
  }, [toolName, tools]);

  if (isLoading) {
    return (
      <div className="flex h-full min-h-0 w-full items-center justify-center rounded-lg border border-zinc-200 bg-white text-sm text-zinc-500 shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40 dark:text-zinc-500 dark:shadow-none">
        Loading tool metadata...
      </div>
    );
  }

  if (errorMessage) {
    return (
      <div className="flex h-full min-h-0 w-full items-center justify-center rounded-lg border border-red-200 bg-red-50 px-6 text-center text-sm text-red-700 shadow-sm dark:border-red-900/60 dark:bg-red-950/20 dark:text-red-300 dark:shadow-none">
        {errorMessage}
      </div>
    );
  }

  if (!tool) {
    return (
      <div className="flex h-full min-h-0 w-full items-center justify-center rounded-lg border border-zinc-200 bg-white px-6 text-center text-sm text-zinc-500 shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40 dark:text-zinc-500 dark:shadow-none">
        Tool '{toolName}' could not be found.
      </div>
    );
  }

  return (
    <div className="flex h-full min-h-0 w-full gap-3">
      <ToolRunPanel tool={tool} />
      <ToolInspectorPanel tool={tool} />
    </div>
  );
}
import { useEffect, useMemo, useState } from 'react';

import { getMcpInfo, type McpInfo } from '../api/mcpApi';
import { McpToolInspectorPanel } from '../components/Mcp/McpToolInspectorPanel';
import { McpToolRunPanel } from '../components/Mcp/McpToolRunPanel';

type McpToolDetailPageProps = {
  toolName: string;
};

export function McpToolDetailPage({ toolName }: McpToolDetailPageProps) {
  const [info, setInfo] = useState<McpInfo | null>(null);
  const [isLoading, setLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  useEffect(() => {
    let isMounted = true;

    async function loadMcpInfo() {
      try {
        setLoading(true);
        setErrorMessage(null);

        const result = await getMcpInfo();

        if (!isMounted) {
          return;
        }

        setInfo(result);
      } catch (error) {
        if (!isMounted) {
          return;
        }

        setErrorMessage(
          error instanceof Error
            ? error.message
            : 'MCP tool metadata could not be loaded.',
        );
      } finally {
        if (isMounted) {
          setLoading(false);
        }
      }
    }

    void loadMcpInfo();

    return () => {
      isMounted = false;
    };
  }, []);

  const tool = useMemo(() => {
    return info?.tools.find(
      (item) => item.name.toLowerCase() === toolName.toLowerCase(),
    );
  }, [info, toolName]);

  if (isLoading) {
    return (
      <div className="flex h-full min-h-0 w-full items-center justify-center rounded-lg border border-zinc-200 bg-white text-sm text-zinc-500 shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40 dark:text-zinc-500 dark:shadow-none">
        Loading MCP tool...
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

  if (!info || !tool) {
    return (
      <div className="flex h-full min-h-0 w-full items-center justify-center rounded-lg border border-zinc-200 bg-white px-6 text-center text-sm text-zinc-500 shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40 dark:text-zinc-500 dark:shadow-none">
        MCP tool '{toolName}' could not be found.
      </div>
    );
  }

  return (
    <div className="flex h-full min-h-0 w-full gap-3">
      <McpToolRunPanel tool={tool} />
      <McpToolInspectorPanel info={info} tool={tool} />
    </div>
  );
}

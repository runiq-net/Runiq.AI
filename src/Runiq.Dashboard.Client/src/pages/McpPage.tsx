import { useEffect, useState } from 'react';
import { Wrench } from 'lucide-react';

import { getMcpInfo, type McpInfo } from '../api/mcpApi';
import { McpServerDetailsPanel } from '../components/Mcp/McpServerDetailsPanel';
import { getDashboardBasePath } from '../dashboardConfig';

export function McpPage() {
  const [info, setInfo] = useState<McpInfo | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setLoading] = useState(true);

  useEffect(() => {
    let isMounted = true;

    getMcpInfo()
      .then((result) => {
        if (!isMounted) {
          return;
        }

        setInfo(result);
        setError(null);
      })
      .catch((exception: unknown) => {
        if (!isMounted) {
          return;
        }

        setError(
          exception instanceof Error
            ? exception.message
            : 'Failed to load MCP server info.',
        );
      })
      .finally(() => {
        if (isMounted) {
          setLoading(false);
        }
      });

    return () => {
      isMounted = false;
    };
  }, []);

  if (isLoading) {
    return (
      <div className="rounded-xl border border-zinc-200 bg-white p-5 text-sm text-zinc-600 shadow-sm dark:border-zinc-800 dark:bg-zinc-950 dark:text-zinc-400">
        Loading MCP server info...
      </div>
    );
  }

  if (error) {
    return (
      <div className="rounded-xl border border-red-200 bg-red-50 p-5 text-sm text-red-700 dark:border-red-900/60 dark:bg-red-950/30 dark:text-red-300">
        {error}
      </div>
    );
  }

  if (!info) {
    return null;
  }

  return (
    <div className="grid gap-5 xl:grid-cols-[minmax(0,1fr)_360px]">
      <div className="min-w-0 space-y-5">
        <section className="rounded-xl border border-zinc-200 bg-white p-5 shadow-sm dark:border-zinc-800 dark:bg-zinc-950">
          <div className="mb-4 flex items-center justify-between gap-3">
            <h3 className="m-0 text-lg font-semibold text-zinc-950 dark:text-zinc-100">
              Tools
            </h3>
            <span className="rounded-full bg-zinc-100 px-2.5 py-1 text-xs font-semibold text-zinc-700 dark:bg-zinc-900 dark:text-zinc-300">
              {info.tools.length}
            </span>
          </div>

          {info.tools.length === 0 ? (
            <div className="rounded-xl border border-dashed border-zinc-300 p-6 text-sm text-zinc-500 dark:border-zinc-800 dark:text-zinc-500">
              No MCP tools detected.
            </div>
          ) : (
            <div className="max-h-[560px] overflow-auto rounded-xl border border-zinc-200 dark:border-zinc-800">
              <table className="w-full min-w-[760px] border-collapse text-left text-sm">
                <thead className="sticky top-0 bg-zinc-50 text-xs uppercase tracking-wide text-zinc-500 dark:bg-zinc-900 dark:text-zinc-500">
                  <tr>
                    <th className="w-[30%] px-4 py-3 font-semibold">Name</th>
                    <th className="px-4 py-3 font-semibold">Description</th>
                    <th className="w-32 px-4 py-3 font-semibold">Source</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-zinc-200 dark:divide-zinc-800">
                  {info.tools.map((tool) => (
                    <tr
                      key={`${tool.source}-${tool.name}`}
                      onClick={() => navigateToMcpTool(tool.name)}
                      className="cursor-pointer align-top transition hover:bg-zinc-50/70 dark:hover:bg-zinc-900/40"
                    >
                      <td className="px-4 py-3">
                        <div className="flex min-w-0 items-center gap-2 font-medium text-zinc-950 dark:text-zinc-100">
                          <Wrench
                            className="shrink-0 text-zinc-500"
                            size={15}
                            aria-hidden="true"
                          />
                          <span className="break-words font-mono text-[13px]">
                            {tool.name}
                          </span>
                        </div>
                      </td>
                      <td className="px-4 py-3 text-zinc-600 dark:text-zinc-400">
                        {tool.description ?? 'No description'}
                      </td>
                      <td className="px-4 py-3">
                        <span className="rounded-full bg-zinc-100 px-2 py-1 text-xs font-medium text-zinc-600 dark:bg-zinc-900 dark:text-zinc-400">
                          {tool.source}
                        </span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </section>
      </div>

      <McpServerDetailsPanel info={info} />
    </div>
  );
}

function navigateToMcpTool(toolName: string) {
  const basePath = getDashboardBasePath().replace(/\/+$/g, '');

  window.history.pushState(
    {},
    '',
    `${basePath}/mcp/${encodeURIComponent(toolName)}`,
  );

  window.dispatchEvent(new PopStateEvent('popstate'));
}

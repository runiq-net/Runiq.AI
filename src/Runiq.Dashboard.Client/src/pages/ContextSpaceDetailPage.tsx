import { useEffect, useMemo, useState } from 'react';

import {
  getContextSpaces,
  type ContextSpaceMetadata,
} from '../api/agentMetadataApi';
import { ContextSpaceInspectorPanel } from '../components/ContextSpaces/ContextSpaceInspectorPanel';
import { ContextSpaceMainPanel } from '../components/ContextSpaces/ContextSpaceMainPanel';
import { getDashboardBasePath } from '../dashboardConfig';

type ContextSpaceDetailPageProps = {
  contextSpaceId: string;
};

export function ContextSpaceDetailPage({
  contextSpaceId,
}: ContextSpaceDetailPageProps) {
  const [contextSpaces, setContextSpaces] = useState<ContextSpaceMetadata[]>([]);
  const [isLoading, setLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  useEffect(() => {
    let isMounted = true;

    async function loadContextSpaces() {
      try {
        setLoading(true);
        setErrorMessage(null);

        const result = await getContextSpaces(getDashboardBasePath());

        if (!isMounted) {
          return;
        }

        setContextSpaces(result);
      } catch (error) {
        if (!isMounted) {
          return;
        }

        setErrorMessage(
          error instanceof Error
            ? error.message
            : 'Context Spaces metadata could not be loaded.',
        );
      } finally {
        if (isMounted) {
          setLoading(false);
        }
      }
    }

    void loadContextSpaces();

    return () => {
      isMounted = false;
    };
  }, []);

  const contextSpace = useMemo(() => {
    return contextSpaces.find(
      (item) => item.id.toLowerCase() === contextSpaceId.toLowerCase(),
    );
  }, [contextSpaceId, contextSpaces]);

  if (isLoading) {
    return (
      <div className="flex h-full min-h-0 w-full items-center justify-center rounded-lg border border-zinc-200 bg-white text-sm text-zinc-500 shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40 dark:text-zinc-500 dark:shadow-none">
        Loading context space metadata...
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

  if (!contextSpace) {
    return (
      <div className="flex h-full min-h-0 w-full items-center justify-center rounded-lg border border-zinc-200 bg-white px-6 text-center text-sm text-zinc-500 shadow-sm dark:border-zinc-800 dark:bg-zinc-950/40 dark:text-zinc-500 dark:shadow-none">
        Context space '{contextSpaceId}' could not be found.
      </div>
    );
  }

  return (
    <div className="flex h-full min-h-0 w-full gap-3">
      <ContextSpaceMainPanel contextSpace={contextSpace} />
      <ContextSpaceInspectorPanel contextSpace={contextSpace} />
    </div>
  );
}

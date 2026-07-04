import { getDashboardBasePath } from '../dashboardConfig';

export type RagLastUpsertInfo = {
  succeeded: boolean;
  errorCode: string;
  reason: string;
  chunkCount: number;
  timestamp: string;
};

export type RagLastRetrievalInfo = {
  succeeded: boolean;
  errorCode: string;
  reason: string;
  resultCount: number;
  durationMilliseconds: number;
  timestamp: string;
};

export type RagInfo = {
  enabled: boolean;
  vectorStore?: string | null;
  indexName?: string | null;
  defaultTopK?: number | null;
  embeddingDimension?: number | null;
  lastUpsert?: RagLastUpsertInfo | null;
  lastRetrieval?: RagLastRetrievalInfo | null;
  diagnostics?: string | null;
};

export async function getRagInfo(): Promise<RagInfo> {
  const basePath = getDashboardBasePath().replace(/\/+$/g, '');
  const response = await fetch(`${basePath}/api/rag`);

  if (!response.ok) {
    throw new Error(`Failed to load RAG info. Status: ${response.status}`);
  }

  return (await response.json()) as RagInfo;
}

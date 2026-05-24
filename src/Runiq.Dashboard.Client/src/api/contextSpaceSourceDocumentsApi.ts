export type ContextSpaceSourceDocumentListItem = {
  relativePath: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  isPreviewSupported: boolean;
};

export type ContextSpaceSourceDocumentGroup = {
  sourceId: string;
  sourceName: string;
  provider: string;
  path?: string | null;
  documentCount: number;
  documents: ContextSpaceSourceDocumentListItem[];
};

export type ContextSpaceSourceDocumentsResponse = {
  contextSpaceId: string;
  sourceGroups: ContextSpaceSourceDocumentGroup[];
};

export type ContextSpaceSourceDocumentPreview = {
  contextSpaceId: string;
  sourceId: string;
  sourceName: string;
  relativePath: string;
  fileName: string;
  contentType: string;
  content: string;
  isTruncated: boolean;
  sizeBytes: number;
};

export async function getContextSpaceSourceDocuments(
  basePath: string,
  contextSpaceId: string,
): Promise<ContextSpaceSourceDocumentsResponse> {
  const response = await fetch(
    `${trimTrailingSlash(basePath)}/api/context-spaces/${encodeURIComponent(contextSpaceId)}/source-documents`,
  );

  if (!response.ok) {
    throw new Error('Context Space source documents could not be loaded.');
  }

  return response.json() as Promise<ContextSpaceSourceDocumentsResponse>;
}

export async function getContextSpaceSourceDocumentPreview(
  basePath: string,
  contextSpaceId: string,
  sourceId: string,
  relativePath: string,
): Promise<ContextSpaceSourceDocumentPreview> {
  const searchParams = new URLSearchParams({
    sourceId,
    path: relativePath,
  });

  const response = await fetch(
    `${trimTrailingSlash(basePath)}/api/context-spaces/${encodeURIComponent(contextSpaceId)}/source-documents/preview?${searchParams.toString()}`,
  );

  if (response.status === 415) {
    throw new Error('Preview is not available for this file type.');
  }

  if (!response.ok) {
    throw new Error('Source document preview could not be loaded.');
  }

  return response.json() as Promise<ContextSpaceSourceDocumentPreview>;
}

function trimTrailingSlash(value: string): string {
  return value.endsWith('/') ? value.slice(0, -1) : value;
}

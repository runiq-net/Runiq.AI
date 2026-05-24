export type ContextSpaceSkillDocumentListItem = {
  skillId: string;
  name: string;
  version?: string | null;
  description?: string | null;
  tags: string[];
  relativePath: string;
  contentType: string;
  isPreviewSupported: boolean;
};

export type ContextSpaceSkillSourceDocumentGroup = {
  sourceId: string;
  sourceName: string;
  provider: string;
  path?: string | null;
  skillCount: number;
  skills: ContextSpaceSkillDocumentListItem[];
};

export type ContextSpaceSkillDocumentsResponse = {
  contextSpaceId: string;
  skillSources: ContextSpaceSkillSourceDocumentGroup[];
};

export type ContextSpaceSkillDocumentPreview = {
  contextSpaceId: string;
  skillId: string;
  name: string;
  version?: string | null;
  description?: string | null;
  tags: string[];
  relativePath: string;
  contentType: string;
  content: string;
  isTruncated: boolean;
  sizeBytes: number;
};

export async function getContextSpaceSkillDocuments(
  basePath: string,
  contextSpaceId: string,
): Promise<ContextSpaceSkillDocumentsResponse> {
  const response = await fetch(
    `${trimTrailingSlash(basePath)}/api/context-spaces/${encodeURIComponent(contextSpaceId)}/skill-documents`,
  );

  if (!response.ok) {
    throw new Error('Context Space skill documents could not be loaded.');
  }

  return response.json() as Promise<ContextSpaceSkillDocumentsResponse>;
}

export async function getContextSpaceSkillDocumentPreview(
  basePath: string,
  contextSpaceId: string,
  skillId: string,
): Promise<ContextSpaceSkillDocumentPreview> {
  const searchParams = new URLSearchParams({
    skillId,
  });

  const response = await fetch(
    `${trimTrailingSlash(basePath)}/api/context-spaces/${encodeURIComponent(contextSpaceId)}/skill-documents/preview?${searchParams.toString()}`,
  );

  if (!response.ok) {
    throw new Error('Skill document preview could not be loaded.');
  }

  return response.json() as Promise<ContextSpaceSkillDocumentPreview>;
}

function trimTrailingSlash(value: string): string {
  return value.endsWith('/') ? value.slice(0, -1) : value;
}

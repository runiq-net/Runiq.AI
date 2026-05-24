import { useEffect, useMemo, useState } from 'react';
import { Check, Copy, FileText, FolderSearch } from 'lucide-react';

import type { ContextSpaceMetadata } from '../../../api/agentMetadataApi';
import {
  getContextSpaceSourceDocumentPreview,
  type ContextSpaceSourceDocumentGroup,
  type ContextSpaceSourceDocumentListItem,
  type ContextSpaceSourceDocumentPreview,
  type ContextSpaceSourceDocumentsResponse,
} from '../../../api/contextSpaceSourceDocumentsApi';
import { getDashboardBasePath } from '../../../dashboardConfig';

type ContextSpaceSourcesTabProps = {
  contextSpace: ContextSpaceMetadata;
  sourceDocuments?: ContextSpaceSourceDocumentsResponse | null;
  isLoading: boolean;
  errorMessage?: string | null;
};

type SelectedDocument = {
  sourceId: string;
  document: ContextSpaceSourceDocumentListItem;
};

export function ContextSpaceSourcesTab({
  contextSpace,
  sourceDocuments,
  isLoading,
  errorMessage,
}: ContextSpaceSourcesTabProps) {
  const [selectedDocument, setSelectedDocument] =
    useState<SelectedDocument | null>(null);
  const [preview, setPreview] =
    useState<ContextSpaceSourceDocumentPreview | null>(null);
  const [previewError, setPreviewError] = useState<string | null>(null);
  const [isPreviewLoading, setPreviewLoading] = useState(false);
  const [copied, setCopied] = useState(false);

  const firstPreviewDocument = useMemo(() => {
    for (const group of sourceDocuments?.sourceGroups ?? []) {
      const document = group.documents.find((item) => item.isPreviewSupported);

      if (document) {
        return {
          sourceId: group.sourceId,
          document,
        };
      }
    }

    return null;
  }, [sourceDocuments]);

  useEffect(() => {
    setSelectedDocument(null);
    setPreview(null);
    setPreviewError(null);
  }, [contextSpace.id]);

  useEffect(() => {
    if (!selectedDocument && firstPreviewDocument) {
      setSelectedDocument(firstPreviewDocument);
    }
  }, [firstPreviewDocument, selectedDocument]);

  useEffect(() => {
    let isMounted = true;

    async function loadPreview() {
      if (!selectedDocument) {
        setPreview(null);
        setPreviewError(null);
        return;
      }

      if (!selectedDocument.document.isPreviewSupported) {
        setPreview(null);
        setPreviewError('Preview is not available for this file type.');
        return;
      }

      try {
        setPreviewLoading(true);
        setPreviewError(null);
        setPreview(null);

        const result = await getContextSpaceSourceDocumentPreview(
          getDashboardBasePath(),
          contextSpace.id,
          selectedDocument.sourceId,
          selectedDocument.document.relativePath,
        );

        if (isMounted) {
          setPreview(result);
        }
      } catch (error) {
        if (isMounted) {
          setPreviewError(
            error instanceof Error
              ? error.message
              : 'Source document preview could not be loaded.',
          );
        }
      } finally {
        if (isMounted) {
          setPreviewLoading(false);
        }
      }
    }

    void loadPreview();

    return () => {
      isMounted = false;
    };
  }, [contextSpace.id, selectedDocument]);

  async function handleCopy() {
    if (!preview?.content) {
      return;
    }

    try {
      await navigator.clipboard.writeText(preview.content);
      setCopied(true);
      window.setTimeout(() => setCopied(false), 1200);
    } catch {
      setCopied(false);
    }
  }

  if (isLoading) {
    return <StatePanel message="Loading source documents..." />;
  }

  if (errorMessage) {
    return <StatePanel tone="error" message={errorMessage} />;
  }

  if (contextSpace.sources.length === 0) {
    return <StatePanel message="No sources attached." />;
  }

  const sourceGroups = sourceDocuments?.sourceGroups ?? [];
  const totalDocumentCount = sourceGroups.reduce(
    (sum, group) => sum + group.documentCount,
    0,
  );

  return (
    <div className="grid h-full min-h-0 grid-rows-[minmax(180px,35%)_minmax(0,1fr)] gap-3 xl:grid-cols-[minmax(280px,360px)_1fr] xl:grid-rows-none">
      <section className="flex min-h-0 min-w-0 flex-col overflow-hidden rounded-lg border border-zinc-200 bg-zinc-50 dark:border-zinc-800 dark:bg-zinc-900/30">
        <div className="border-b border-zinc-200 px-3 py-3 dark:border-zinc-800">
          <div className="flex items-center justify-between gap-3">
            <div className="min-w-0">
              <div className="text-sm font-semibold text-zinc-950 dark:text-zinc-100">
                Source documents
              </div>
              <div className="mt-0.5 text-xs text-zinc-500 dark:text-zinc-500">
                {totalDocumentCount} document{totalDocumentCount === 1 ? '' : 's'}
              </div>
            </div>

            <span className="inline-flex size-8 shrink-0 items-center justify-center rounded-lg border border-zinc-200 bg-white text-zinc-600 dark:border-zinc-800 dark:bg-zinc-950 dark:text-zinc-300">
              <FolderSearch className="size-4" />
            </span>
          </div>
        </div>

        <div className="min-h-0 flex-1 space-y-3 overflow-y-auto p-3 [scrollbar-width:thin] [scrollbar-color:rgb(161_161_170)_transparent] dark:[scrollbar-color:rgb(82_82_91)_transparent]">
          {sourceGroups.map((group) => (
            <SourceGroupCard
              key={group.sourceId}
              group={group}
              selectedDocument={selectedDocument}
              onSelectDocument={setSelectedDocument}
            />
          ))}

          {sourceGroups.length > 0 && totalDocumentCount === 0 ? (
            <div className="rounded-md border border-zinc-200 bg-white px-3 py-3 text-sm text-zinc-600 dark:border-zinc-800 dark:bg-zinc-950/50 dark:text-zinc-400">
              No searchable documents found.
            </div>
          ) : null}
        </div>
      </section>

      <section className="min-h-0 min-w-0 overflow-hidden rounded-lg border border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-950/40">
        <DocumentPreviewPanel
          selectedDocument={selectedDocument}
          preview={preview}
          previewError={previewError}
          isPreviewLoading={isPreviewLoading}
          copied={copied}
          onCopy={handleCopy}
        />
      </section>
    </div>
  );
}

function SourceGroupCard({
  group,
  selectedDocument,
  onSelectDocument,
}: {
  group: ContextSpaceSourceDocumentGroup;
  selectedDocument: SelectedDocument | null;
  onSelectDocument: (document: SelectedDocument) => void;
}) {
  return (
    <article className="overflow-hidden rounded-md border border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-950/50">
      <div className="border-b border-zinc-200 px-3 py-3 dark:border-zinc-800">
        <div className="flex items-start gap-2">
          <span className="mt-0.5 inline-flex size-7 shrink-0 items-center justify-center rounded-md border border-zinc-200 bg-zinc-50 text-zinc-600 dark:border-zinc-800 dark:bg-zinc-900 dark:text-zinc-300">
            <FileText className="size-3.5" />
          </span>

          <div className="min-w-0 flex-1">
            <div className="truncate text-base font-semibold text-zinc-950 dark:text-zinc-100">
              {group.sourceName || group.sourceId}
            </div>

            <div className="mt-1 flex flex-wrap items-center gap-1.5">
              <span className="text-xs font-medium text-zinc-600 dark:text-zinc-400">
                {formatProvider(group.provider)} · {group.documentCount} document{group.documentCount === 1 ? '' : 's'}
              </span>
            </div>

            {group.path ? (
              <div className="mt-2 truncate font-mono text-xs text-zinc-500 dark:text-zinc-500">
                {group.path}
              </div>
            ) : null}
          </div>
        </div>
      </div>

      <div className="max-h-80 overflow-y-auto p-2 [scrollbar-width:thin] [scrollbar-color:rgb(161_161_170)_transparent] dark:[scrollbar-color:rgb(82_82_91)_transparent]">
        {group.documents.length === 0 ? (
          <div className="px-2 py-2 text-sm text-zinc-500 dark:text-zinc-500">
            No searchable documents found.
          </div>
        ) : (
          <div className="space-y-1">
            {group.documents.map((document) => {
              const isSelected =
                selectedDocument?.sourceId === group.sourceId &&
                selectedDocument.document.relativePath === document.relativePath;

              return (
                <button
                  key={document.relativePath}
                  type="button"
                  onClick={() => onSelectDocument({
                    sourceId: group.sourceId,
                    document,
                  })}
                  className={[
                    'flex w-full items-center gap-2 rounded-md px-2 py-2 text-left text-sm transition',
                    isSelected
                      ? 'bg-zinc-950 text-white dark:bg-zinc-100 dark:text-zinc-950'
                      : 'text-zinc-700 hover:bg-zinc-100 dark:text-zinc-300 dark:hover:bg-zinc-900',
                  ].join(' ')}
                >
                  <FileText className="size-3.5 shrink-0" />
                  <span className="min-w-0 flex-1 truncate">
                    {document.fileName || document.relativePath}
                  </span>
                  {!document.isPreviewSupported ? (
                    <span className="shrink-0 text-[11px] opacity-70">
                      No preview
                    </span>
                  ) : null}
                </button>
              );
            })}
          </div>
        )}
      </div>
    </article>
  );
}

function DocumentPreviewPanel({
  selectedDocument,
  preview,
  previewError,
  isPreviewLoading,
  copied,
  onCopy,
}: {
  selectedDocument: SelectedDocument | null;
  preview: ContextSpaceSourceDocumentPreview | null;
  previewError: string | null;
  isPreviewLoading: boolean;
  copied: boolean;
  onCopy: () => void;
}) {
  if (!selectedDocument) {
    return <StatePanel message="Select a document to preview." unframed />;
  }

  return (
    <div className="flex h-full min-h-0 flex-col">
      <div className="border-b border-zinc-200 px-4 py-3 dark:border-zinc-800">
        <div className="flex items-start justify-between gap-3">
          <div className="min-w-0">
            <div className="truncate text-sm font-semibold text-zinc-950 dark:text-zinc-100">
              {selectedDocument.document.fileName}
            </div>
            <div className="mt-1 truncate font-mono text-xs text-zinc-500 dark:text-zinc-500">
              {preview?.relativePath ?? selectedDocument.document.relativePath}
            </div>
          </div>

          <button
            type="button"
            onClick={onCopy}
            disabled={!preview?.content}
            className="inline-flex size-8 shrink-0 items-center justify-center rounded-lg border border-zinc-200 text-zinc-500 transition hover:bg-zinc-100 hover:text-zinc-950 disabled:cursor-not-allowed disabled:opacity-50 dark:border-zinc-800 dark:text-zinc-400 dark:hover:bg-zinc-900 dark:hover:text-zinc-100"
            aria-label="Copy document content"
            title={copied ? 'Copied' : 'Copy'}
          >
            {copied ? <Check className="size-4" /> : <Copy className="size-4" />}
          </button>
        </div>

        <div className="mt-2 flex flex-wrap items-center gap-2 text-xs text-zinc-500 dark:text-zinc-500">
          <span>{preview?.sourceName ?? selectedDocument.sourceId}</span>
          <span>·</span>
          <span>{preview?.contentType ?? selectedDocument.document.contentType}</span>
          <span>·</span>
          <span>{formatBytes(preview?.sizeBytes ?? selectedDocument.document.sizeBytes)}</span>
          {preview?.isTruncated ? (
            <>
              <span>·</span>
              <span className="text-amber-700 dark:text-amber-300">
                Preview truncated
              </span>
            </>
          ) : null}
        </div>
      </div>

      <div className="min-h-0 flex-1 overflow-auto bg-zinc-50 p-4 dark:bg-zinc-950 [scrollbar-width:thin] [scrollbar-color:rgb(161_161_170)_transparent] dark:[scrollbar-color:rgb(82_82_91)_transparent]">
        {isPreviewLoading ? (
          <div className="text-sm text-zinc-500 dark:text-zinc-500">
            Loading preview...
          </div>
        ) : previewError ? (
          <div className="text-sm text-zinc-600 dark:text-zinc-400">
            {previewError}
          </div>
        ) : preview ? (
          <pre className="whitespace-pre-wrap break-words font-mono text-xs leading-6 text-zinc-800 dark:text-zinc-200">
            {preview.content}
          </pre>
        ) : (
          <div className="text-sm text-zinc-500 dark:text-zinc-500">
            Preview is not available for this file type.
          </div>
        )}
      </div>
    </div>
  );
}

function StatePanel({
  message,
  tone = 'neutral',
  unframed = false,
}: {
  message: string;
  tone?: 'neutral' | 'error';
  unframed?: boolean;
}) {
  const className = tone === 'error'
    ? 'border-red-200 bg-red-50 text-red-700 dark:border-red-900/60 dark:bg-red-950/20 dark:text-red-300'
    : 'border-zinc-200 bg-zinc-50 text-zinc-600 dark:border-zinc-800 dark:bg-zinc-900/30 dark:text-zinc-400';

  if (unframed) {
    return (
      <div className="p-5 text-sm text-zinc-600 dark:text-zinc-400">
        {message}
      </div>
    );
  }

  return (
    <div className={`rounded-lg border p-5 text-sm ${className}`}>
      {message}
    </div>
  );
}

function formatProvider(value: string): string {
  return value
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    .replace(/[-_]+/g, ' ')
    .trim();
}

function formatBytes(value: number): string {
  if (value < 1024) {
    return `${value} B`;
  }

  if (value < 1024 * 1024) {
    return `${(value / 1024).toFixed(1)} KB`;
  }

  return `${(value / (1024 * 1024)).toFixed(1)} MB`;
}

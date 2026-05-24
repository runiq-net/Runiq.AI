import { useEffect, useMemo, useState } from 'react';
import { Check, Copy, FileText, Sparkles } from 'lucide-react';

import type { ContextSpaceMetadata } from '../../../api/agentMetadataApi';
import {
  getContextSpaceSkillDocumentPreview,
  type ContextSpaceSkillDocumentListItem,
  type ContextSpaceSkillDocumentPreview,
  type ContextSpaceSkillDocumentsResponse,
  type ContextSpaceSkillSourceDocumentGroup,
} from '../../../api/contextSpaceSkillDocumentsApi';
import { getDashboardBasePath } from '../../../dashboardConfig';

type ContextSpaceSkillsTabProps = {
  contextSpace: ContextSpaceMetadata;
  skillDocuments?: ContextSpaceSkillDocumentsResponse | null;
  isLoading: boolean;
  errorMessage?: string | null;
};

type SelectedSkill = {
  sourceId: string;
  skill: ContextSpaceSkillDocumentListItem;
};

export function ContextSpaceSkillsTab({
  contextSpace,
  skillDocuments,
  isLoading,
  errorMessage,
}: ContextSpaceSkillsTabProps) {
  const [selectedSkill, setSelectedSkill] = useState<SelectedSkill | null>(null);
  const [preview, setPreview] =
    useState<ContextSpaceSkillDocumentPreview | null>(null);
  const [previewError, setPreviewError] = useState<string | null>(null);
  const [isPreviewLoading, setPreviewLoading] = useState(false);
  const [copied, setCopied] = useState(false);

  const firstPreviewSkill = useMemo(() => {
    for (const source of skillDocuments?.skillSources ?? []) {
      const skill = source.skills.find((item) => item.isPreviewSupported);

      if (skill) {
        return {
          sourceId: source.sourceId,
          skill,
        };
      }
    }

    return null;
  }, [skillDocuments]);

  useEffect(() => {
    setSelectedSkill(null);
    setPreview(null);
    setPreviewError(null);
  }, [contextSpace.id]);

  useEffect(() => {
    if (!selectedSkill && firstPreviewSkill) {
      setSelectedSkill(firstPreviewSkill);
    }
  }, [firstPreviewSkill, selectedSkill]);

  useEffect(() => {
    let isMounted = true;

    async function loadPreview() {
      if (!selectedSkill) {
        setPreview(null);
        setPreviewError(null);
        return;
      }

      if (!selectedSkill.skill.isPreviewSupported) {
        setPreview(null);
        setPreviewError('Preview is not available for this skill.');
        return;
      }

      try {
        setPreviewLoading(true);
        setPreviewError(null);
        setPreview(null);

        const result = await getContextSpaceSkillDocumentPreview(
          getDashboardBasePath(),
          contextSpace.id,
          selectedSkill.skill.skillId,
        );

        if (isMounted) {
          setPreview(result);
        }
      } catch (error) {
        if (isMounted) {
          setPreviewError(
            error instanceof Error
              ? error.message
              : 'Skill document preview could not be loaded.',
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
  }, [contextSpace.id, selectedSkill]);

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
    return <StatePanel message="Loading skills..." />;
  }

  if (errorMessage) {
    return <StatePanel tone="error" message={errorMessage} />;
  }

  if (contextSpace.skills.length === 0) {
    return <StatePanel message="No skills discovered." />;
  }

  const skillSources = skillDocuments?.skillSources ?? [];
  const totalSkillCount = skillSources.reduce(
    (sum, source) => sum + source.skillCount,
    0,
  );

  return (
    <div className="grid h-full min-h-0 grid-rows-[minmax(180px,35%)_minmax(0,1fr)] gap-3 xl:grid-cols-[minmax(280px,360px)_1fr] xl:grid-rows-none">
      <section className="flex min-h-0 min-w-0 flex-col overflow-hidden rounded-lg border border-zinc-200 bg-zinc-50 dark:border-zinc-800 dark:bg-zinc-900/30">
        <div className="border-b border-zinc-200 px-3 py-3 dark:border-zinc-800">
          <div className="flex items-center justify-between gap-3">
            <div className="min-w-0">
              <div className="text-sm font-semibold text-zinc-950 dark:text-zinc-100">
                Skill documents
              </div>
              <div className="mt-0.5 text-xs text-zinc-500 dark:text-zinc-500">
                {totalSkillCount} skill{totalSkillCount === 1 ? '' : 's'}
              </div>
              <div className="mt-1 text-xs leading-5 text-zinc-500 dark:text-zinc-500">
                Skills are added to model instructions during agent runs.
              </div>
            </div>

            <span className="inline-flex size-8 shrink-0 items-center justify-center rounded-lg border border-zinc-200 bg-white text-zinc-600 dark:border-zinc-800 dark:bg-zinc-950 dark:text-zinc-300">
              <Sparkles className="size-4" />
            </span>
          </div>
        </div>

        <div className="min-h-0 flex-1 space-y-3 overflow-y-auto p-3 [scrollbar-width:thin] [scrollbar-color:rgb(161_161_170)_transparent] dark:[scrollbar-color:rgb(82_82_91)_transparent]">
          {skillSources.map((source) => (
            <SkillSourceCard
              key={source.sourceId}
              source={source}
              selectedSkill={selectedSkill}
              onSelectSkill={setSelectedSkill}
            />
          ))}
        </div>
      </section>

      <section className="min-h-0 min-w-0 overflow-hidden rounded-lg border border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-950/40">
        <SkillPreviewPanel
          selectedSkill={selectedSkill}
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

function SkillSourceCard({
  source,
  selectedSkill,
  onSelectSkill,
}: {
  source: ContextSpaceSkillSourceDocumentGroup;
  selectedSkill: SelectedSkill | null;
  onSelectSkill: (skill: SelectedSkill) => void;
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
              {source.sourceName || source.sourceId}
            </div>

            <div className="mt-1 text-xs font-medium text-zinc-600 dark:text-zinc-400">
              {formatProvider(source.provider)} {'\u00b7'} {source.skillCount} skill{source.skillCount === 1 ? '' : 's'}
            </div>

            {source.path ? (
              <div className="mt-2 truncate font-mono text-xs text-zinc-500 dark:text-zinc-500">
                {source.path}
              </div>
            ) : null}
          </div>
        </div>
      </div>

      <div className="max-h-80 overflow-y-auto p-2 [scrollbar-width:thin] [scrollbar-color:rgb(161_161_170)_transparent] dark:[scrollbar-color:rgb(82_82_91)_transparent]">
        {source.skills.length === 0 ? (
          <div className="px-2 py-2 text-sm text-zinc-500 dark:text-zinc-500">
            No skills discovered.
          </div>
        ) : (
          <div className="space-y-1">
            {source.skills.map((skill) => {
              const isSelected =
                selectedSkill?.sourceId === source.sourceId &&
                selectedSkill.skill.skillId === skill.skillId;

              return (
                <button
                  key={skill.skillId}
                  type="button"
                  onClick={() => onSelectSkill({
                    sourceId: source.sourceId,
                    skill,
                  })}
                  className={[
                    'flex w-full items-center gap-2 rounded-md px-2 py-2 text-left text-sm transition',
                    isSelected
                      ? 'bg-zinc-950 text-white dark:bg-zinc-100 dark:text-zinc-950'
                      : 'text-zinc-700 hover:bg-zinc-100 dark:text-zinc-300 dark:hover:bg-zinc-900',
                  ].join(' ')}
                >
                  <Sparkles className="size-3.5 shrink-0" />
                  <span className="min-w-0 flex-1 truncate">
                    {skill.name || skill.skillId}
                  </span>
                  {skill.version ? (
                    <span className="shrink-0 text-[11px] opacity-70">
                      v{skill.version}
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

function SkillPreviewPanel({
  selectedSkill,
  preview,
  previewError,
  isPreviewLoading,
  copied,
  onCopy,
}: {
  selectedSkill: SelectedSkill | null;
  preview: ContextSpaceSkillDocumentPreview | null;
  previewError: string | null;
  isPreviewLoading: boolean;
  copied: boolean;
  onCopy: () => void;
}) {
  if (!selectedSkill) {
    return <StatePanel message="Select a skill to preview." unframed />;
  }

  const metadata = preview ?? selectedSkill.skill;

  return (
    <div className="flex h-full min-h-0 flex-col">
      <div className="border-b border-zinc-200 px-4 py-3 dark:border-zinc-800">
        <div className="flex items-start justify-between gap-3">
          <div className="min-w-0">
            <div className="flex min-w-0 flex-wrap items-center gap-2">
              <div className="truncate text-sm font-semibold text-zinc-950 dark:text-zinc-100">
                {metadata.name || selectedSkill.skill.skillId}
              </div>
              {metadata.version ? (
                <span className="rounded-md border border-zinc-200 px-2 py-0.5 text-xs text-zinc-600 dark:border-zinc-800 dark:text-zinc-400">
                  v{metadata.version}
                </span>
              ) : null}
            </div>
            <div className="mt-1 truncate font-mono text-xs text-zinc-500 dark:text-zinc-500">
              {metadata.relativePath}
            </div>
          </div>

          <button
            type="button"
            onClick={onCopy}
            disabled={!preview?.content}
            className="inline-flex size-8 shrink-0 items-center justify-center rounded-lg border border-zinc-200 text-zinc-500 transition hover:bg-zinc-100 hover:text-zinc-950 disabled:cursor-not-allowed disabled:opacity-50 dark:border-zinc-800 dark:text-zinc-400 dark:hover:bg-zinc-900 dark:hover:text-zinc-100"
            aria-label="Copy skill content"
            title={copied ? 'Copied' : 'Copy'}
          >
            {copied ? <Check className="size-4" /> : <Copy className="size-4" />}
          </button>
        </div>

        <div className="mt-3 space-y-2">
          <p className="text-sm leading-5 text-zinc-600 dark:text-zinc-400">
            {metadata.description || 'No description.'}
          </p>

          {metadata.tags.length > 0 ? (
            <div className="flex flex-wrap gap-1.5">
              {metadata.tags.map((tag) => (
                <span
                  key={tag}
                  className="rounded-full border border-zinc-200 px-2 py-0.5 text-xs text-zinc-600 dark:border-zinc-800 dark:text-zinc-400"
                >
                  {tag}
                </span>
              ))}
            </div>
          ) : null}

          <div className="flex flex-wrap items-center gap-2 text-xs text-zinc-500 dark:text-zinc-500">
            <span>{preview?.contentType ?? selectedSkill.skill.contentType}</span>
            {preview?.sizeBytes ? (
              <>
                <span>{'\u00b7'}</span>
                <span>{formatBytes(preview.sizeBytes)}</span>
              </>
            ) : null}
            {preview?.isTruncated ? (
              <>
                <span>{'\u00b7'}</span>
                <span className="text-amber-700 dark:text-amber-300">
                  Preview truncated
                </span>
              </>
            ) : null}
          </div>
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
            Preview is not available for this skill.
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

import { useState } from 'react';
import {
  AlertCircle,
  CheckCircle2,
  ChevronDown,
  ChevronRight,
  Database,
  LoaderCircle,
} from 'lucide-react';

import type { AgentChatRagLifecycle } from '../../../types/agentChat';
import {
  formatRagDuration,
  getDistinctEffectiveQuery,
  getFailureClassificationLabel,
  getNoContextLabel,
  getRejectionReasonLabel,
} from './ragTimeline';

type RagSearchCardProps = {
  lifecycle: AgentChatRagLifecycle;
};

export function RagSearchCard({ lifecycle }: RagSearchCardProps) {
  const [isOpen, setIsOpen] = useState(false);
  const payload = lifecycle.payload;
  const isRunning = lifecycle.status === 'running';
  const isFailed = lifecycle.status === 'failed';

  return (
    <div className="w-full min-w-0 overflow-hidden rounded-xl border border-zinc-200 bg-white text-xs shadow-sm dark:border-zinc-800 dark:bg-zinc-950/60 dark:shadow-none">
      <button
        type="button"
        aria-expanded={isOpen}
        onClick={() => setIsOpen((current) => !current)}
        className="flex min-h-11 w-full min-w-0 flex-wrap items-center gap-2 px-3 py-2 text-left transition hover:bg-zinc-50 focus-visible:outline-2 focus-visible:outline-offset-[-2px] focus-visible:outline-zinc-500 dark:hover:bg-zinc-900/70"
      >
        {isOpen ? <ChevronDown className="size-3.5 shrink-0 text-zinc-400" /> : <ChevronRight className="size-3.5 shrink-0 text-zinc-400" />}

        <span className={[
          'inline-flex size-6 shrink-0 items-center justify-center rounded-full',
          isFailed
            ? 'bg-red-100 text-red-600 dark:bg-red-400/10 dark:text-red-300'
            : 'bg-sky-100 text-sky-700 dark:bg-sky-400/10 dark:text-sky-300',
        ].join(' ')}>
          {isRunning ? <LoaderCircle className="size-3.5 animate-spin" /> : isFailed ? <AlertCircle className="size-3.5" /> : <Database className="size-3.5" />}
        </span>

        <span className="min-w-0 flex-1">
          <span className="block truncate text-sm font-semibold text-zinc-950 dark:text-zinc-100">RAG search</span>
          <span className="block truncate text-[11px] text-zinc-500 dark:text-zinc-400" title={payload.indexName}>{payload.indexName}</span>
        </span>

        <Summary lifecycle={lifecycle} />
        <StatusBadge lifecycle={lifecycle} />
      </button>

      {isOpen && <RagDetails lifecycle={lifecycle} />}
    </div>
  );
}

function Summary({ lifecycle }: RagSearchCardProps) {
  if (lifecycle.status === 'running') {
    return <span className="shrink-0 text-[11px] text-zinc-500">{lifecycle.payload.requestedCandidateCount} requested</span>;
  }

  if (lifecycle.status === 'failed') {
    return <span className="shrink-0 text-[11px] text-zinc-500">{formatRagDuration(lifecycle.payload.duration)}</span>;
  }

  const payload = lifecycle.payload;
  return (
    <span className="order-3 w-full min-w-0 text-[11px] text-zinc-500 sm:order-none sm:w-auto sm:shrink-0">
      {formatRagDuration(payload.duration)} · {payload.actualCandidateCount} candidates · {payload.acceptedCount} accepted · {payload.rejectedCount} rejected
      {payload.noContextReason ? ` · ${getNoContextLabel(payload.noContextReason)}` : ''}
    </span>
  );
}

function StatusBadge({ lifecycle }: RagSearchCardProps) {
  const config = lifecycle.status === 'running'
    ? { label: 'Searching', classes: 'border-amber-200 bg-amber-50 text-amber-700 dark:border-amber-900/60 dark:bg-amber-950/30 dark:text-amber-300' }
    : lifecycle.status === 'failed'
      ? { label: 'Failed', classes: 'border-red-200 bg-red-50 text-red-700 dark:border-red-900/60 dark:bg-red-950/30 dark:text-red-300' }
      : { label: lifecycle.payload.noContextReason ? 'Completed · No context' : 'Completed', classes: 'border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-900/60 dark:bg-emerald-950/30 dark:text-emerald-300' };

  return (
    <span className={`ml-auto inline-flex shrink-0 items-center gap-1.5 rounded-full border px-2.5 py-0.5 text-[11px] font-medium ${config.classes}`}>
      {lifecycle.status === 'running' ? <span className="size-1.5 animate-pulse rounded-full bg-current" /> : lifecycle.status === 'completed' ? <CheckCircle2 className="size-3" /> : <AlertCircle className="size-3" />}
      {config.label}
    </span>
  );
}

function RagDetails({ lifecycle }: RagSearchCardProps) {
  if (lifecycle.status === 'completed') {
    const payload = lifecycle.payload;
    return (
      <div className="min-w-0 space-y-4 border-t border-zinc-200 bg-zinc-50/70 px-3 py-3 dark:border-zinc-800 dark:bg-zinc-950">
        <dl className="grid min-w-0 grid-cols-1 gap-x-4 gap-y-2 sm:grid-cols-2">
          <QueryDetails payload={payload} />
          <Detail label="Requested candidates" value={String(payload.requestedCandidateCount)} />
          <Detail label="Maximum accepted" value={String(payload.maximumAcceptedResultCount)} />
          <Detail label="Top raw score" value={formatOptionalNumber(payload.topRawScore)} />
          <Detail label="Top relevance" value={formatOptionalNumber(payload.topNormalizedRelevance)} />
        </dl>
        <ResultList title="Selected results" emptyText="No results selected." results={payload.selectedResults} />
        <ResultList title="Rejected results" emptyText="No results rejected." results={payload.rejectedResults.map((result) => ({ ...result, reason: getRejectionReasonLabel(result.reason) }))} />
      </div>
    );
  }

  if (lifecycle.status === 'failed') {
    const payload = lifecycle.payload;
    return (
      <div className="min-w-0 space-y-4 border-t border-zinc-200 bg-zinc-50/70 px-3 py-3 dark:border-zinc-800 dark:bg-zinc-950">
        <dl className="grid min-w-0 grid-cols-1 gap-x-4 gap-y-2 sm:grid-cols-2">
          <QueryDetails payload={payload} />
          <Detail label="Requested candidates" value={String(payload.requestedCandidateCount)} />
          <Detail label="Duration" value={formatRagDuration(payload.duration)} />
          <Detail label="Failure classification" value={getFailureClassificationLabel(payload.failureClassification)} />
        </dl>
      </div>
    );
  }

  const payload = lifecycle.payload;

  return (
    <div className="min-w-0 space-y-4 border-t border-zinc-200 bg-zinc-50/70 px-3 py-3 dark:border-zinc-800 dark:bg-zinc-950">
      <dl className="grid min-w-0 grid-cols-1 gap-x-4 gap-y-2 sm:grid-cols-2">
        <QueryDetails payload={payload} />
        <Detail label="Requested candidates" value={String(payload.requestedCandidateCount)} />
      </dl>
    </div>
  );
}

function QueryDetails({ payload }: { payload: AgentChatRagLifecycle['payload'] }) {
  const originalQuery = payload.originalQuery ?? 'Redacted';
  const effectiveQuery = getDistinctEffectiveQuery(originalQuery, payload.effectiveQuery);
  return <><Detail label="Original query" value={originalQuery} wide />{effectiveQuery && <Detail label="Effective query" value={effectiveQuery} wide />}</>;
}

function Detail({ label, value, wide = false }: { label: string; value: string; wide?: boolean }) {
  return <div className={`min-w-0 ${wide ? 'sm:col-span-2' : ''}`}><dt className="text-[11px] font-medium uppercase tracking-wide text-zinc-500">{label}</dt><dd className="mt-0.5 break-words text-zinc-900 dark:text-zinc-100">{value}</dd></div>;
}

function ResultList({ title, emptyText, results }: { title: string; emptyText: string; results: Array<{ documentId: string; chunkId: string; reason?: string; contentPreview?: string; previewTruncated?: boolean }> }) {
  return <div className="min-w-0"><h4 className="font-semibold text-zinc-950 dark:text-zinc-100">{title}</h4>{results.length === 0 ? <p className="mt-1 text-zinc-500">{emptyText}</p> : <ul className="mt-2 space-y-1.5">{results.map((result, index) => <li key={`${result.documentId}:${result.chunkId}:${index}`} className="min-w-0 rounded-lg border border-zinc-200 bg-white px-2.5 py-2 dark:border-zinc-800 dark:bg-zinc-900/50"><div className="break-all font-mono text-[11px]">{result.documentId} / {result.chunkId}</div>{result.reason && <div className="mt-1 text-[11px] text-zinc-500">{result.reason}</div>}{result.contentPreview && <details className="mt-2"><summary className="cursor-pointer font-medium">Content preview{result.previewTruncated ? ' (truncated)' : ''}</summary><pre className="mt-1 whitespace-pre-wrap break-words font-sans text-[11px]">{result.contentPreview}</pre></details>}</li>)}</ul>}</div>;
}

function formatOptionalNumber(value: number | undefined): string {
  return value === undefined ? '—' : String(value);
}

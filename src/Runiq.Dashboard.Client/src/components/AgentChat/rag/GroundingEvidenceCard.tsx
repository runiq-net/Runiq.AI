import { ChevronDown, ChevronRight, FileCheck2, Info } from 'lucide-react';
import { useState } from 'react';
import type { AgentChatRagLifecycle } from '../../../types/agentChat';
import { formatRagDuration, getNoContextLabel, getRejectionReasonLabel } from './ragTimeline';

export function GroundingEvidenceCard({ lifecycles }: { lifecycles: readonly AgentChatRagLifecycle[] }) {
  const completed = lifecycles.filter((item): item is Extract<AgentChatRagLifecycle, { status: 'completed' }> => item.status === 'completed');
  if (completed.length === 0) return null;
  return <div className="mt-4 space-y-2">{completed.map((lifecycle) => <Evidence key={lifecycle.payload.correlationId} lifecycle={lifecycle} />)}</div>;
}

function Evidence({ lifecycle }: { lifecycle: Extract<AgentChatRagLifecycle, { status: 'completed' }> }) {
  const [open, setOpen] = useState(false);
  const [rejectedOpen, setRejectedOpen] = useState(false);
  const payload = lifecycle.payload;
  const grounded = payload.selectedResults.length > 0;
  return <section className="min-w-0 overflow-hidden rounded-xl border border-emerald-200 bg-emerald-50/50 text-xs dark:border-emerald-900/60 dark:bg-emerald-950/10">
    <button type="button" aria-expanded={open} onClick={() => setOpen((value) => !value)} className="flex w-full min-w-0 flex-wrap items-center gap-2 px-3 py-2.5 text-left focus-visible:outline-2 focus-visible:outline-emerald-600">
      {open ? <ChevronDown className="size-3.5 shrink-0" /> : <ChevronRight className="size-3.5 shrink-0" />}
      {grounded ? <FileCheck2 className="size-4 shrink-0 text-emerald-700 dark:text-emerald-300" /> : <Info className="size-4 shrink-0 text-sky-700 dark:text-sky-300" />}
      <span className="min-w-0 flex-1"><span className="block font-semibold">{grounded ? `Grounded with ${payload.selectedResults.length} sources` : 'No grounding context'}</span><span className="block truncate text-zinc-600 dark:text-zinc-400">Index: {payload.indexName}</span></span>
      <span className="text-zinc-500">{payload.acceptedCount} accepted</span>
    </button>
    {open && <div className="space-y-3 border-t border-emerald-200 px-3 py-3 dark:border-emerald-900/60"><p className="text-zinc-600 dark:text-zinc-400">These sources were included in the model context. Retrieval relevance is not an answer-quality measure.</p><dl className="grid gap-2 sm:grid-cols-2"><Detail label="Retrieval" value={payload.correlationId} /><Detail label="Duration" value={formatRagDuration(payload.duration)} /><Detail label="Candidates" value={String(payload.actualCandidateCount)} /><Detail label="Rejected" value={String(payload.rejectedCount)} /></dl>{grounded ? <Results results={payload.selectedResults} /> : <p className="text-zinc-600 dark:text-zinc-400">{getNoContextLabel(payload.noContextReason ?? 'Unknown')}</p>}{payload.rejectedResults.length > 0 && <><button type="button" aria-expanded={rejectedOpen} onClick={() => setRejectedOpen((value) => !value)} className="font-medium underline focus-visible:outline-2 focus-visible:outline-emerald-600">{rejectedOpen ? 'Hide rejected candidates' : `Show ${payload.rejectedResults.length} rejected candidates`}</button>{rejectedOpen && <ul className="space-y-2">{payload.rejectedResults.map((result, index) => <li key={`${result.documentId}:${result.chunkId}:${index}`} className="min-w-0 rounded-lg border border-zinc-200 bg-white p-2 dark:border-zinc-800 dark:bg-zinc-900"><span className="block break-all font-mono">{result.documentId} / {result.chunkId}</span><span className="text-zinc-500">{getRejectionReasonLabel(result.reason)}{score(result.normalizedRelevance, result.rawScore)}</span>{result.contentPreview && <Preview value={result.contentPreview} truncated={result.previewTruncated} />}</li>)}</ul>}</>}</div>}
  </section>;
}
function Results({ results }: { results: Extract<AgentChatRagLifecycle, { status: 'completed' }>['payload']['selectedResults'] }) { return <div><h4 className="font-semibold">Sources used</h4><ul className="mt-2 space-y-2">{results.map((result) => <li key={`${result.documentId}:${result.chunkId}:${result.contextOrder}`} className="min-w-0 rounded-lg border border-zinc-200 bg-white p-2 dark:border-zinc-800 dark:bg-zinc-900"><span className="block break-all font-mono">{result.documentId} / {result.chunkId}</span><span className="text-zinc-500">Context order {result.contextOrder + 1} · Included in model context{score(result.normalizedRelevance, result.rawScore)}{result.metric ? ` · Metric: ${result.metric}${result.higherIsBetter === undefined ? "" : result.higherIsBetter ? " (higher scores rank first)" : " (lower scores rank first)"}` : ''}</span>{result.contentPreview && <Preview value={result.contentPreview} truncated={result.previewTruncated} />}</li>)}</ul></div>; }
function Preview({ value, truncated }: { value: string; truncated?: boolean }) { return <details className="mt-2"><summary className="cursor-pointer font-medium">Content preview{truncated ? ' (truncated)' : ''}</summary><pre className="mt-1 whitespace-pre-wrap break-words font-sans">{value}</pre></details>; }
function Detail({ label, value }: { label: string; value: string }) { return <div className="min-w-0"><dt className="uppercase tracking-wide text-zinc-500">{label}</dt><dd className="break-all">{value}</dd></div>; }
function score(relevance: number | undefined, raw: number | undefined) { const values: string[] = []; if (typeof relevance === 'number' && Number.isFinite(relevance)) values.push(`Relevance ${(relevance * 100).toFixed(1)}%`); if (typeof raw === 'number' && Number.isFinite(raw)) values.push(`Raw score ${raw.toFixed(3)}`); return values.length ? ` · ${values.join(' · ')}` : ''; }

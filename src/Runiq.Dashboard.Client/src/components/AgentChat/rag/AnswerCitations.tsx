import { useState } from 'react';
import type { AgentChatCitation } from '../../../types/agentChat';
import { splitCitationMarkers } from './citationMarkers';

type AnswerCitationsProps = {
  content: string;
  citations: readonly AgentChatCitation[];
};

export function AnswerWithCitations({ content, citations }: AnswerCitationsProps) {
  const citationByNumber = new Map(citations.map((citation) => [citation.number, citation]));
  const parts = splitCitationMarkers(content);

  return <div className="whitespace-pre-wrap break-words">{parts.map((part, index) => {
    const citation = part.number === undefined ? undefined : citationByNumber.get(part.number);
    if (!citation) return <span key={index}>{part.text}</span>;
    return <button key={index} type="button" className="mx-0.5 inline rounded font-semibold text-emerald-700 underline decoration-dotted underline-offset-2 focus-visible:outline-2 focus-visible:outline-emerald-600 dark:text-emerald-300" title={`${citation.documentId} / ${citation.chunkId}`} aria-label={`Citation ${citation.number}: ${citation.documentId}, chunk ${citation.chunkId}`} onClick={() => document.getElementById(sourceId(citation))?.focus()}>{part.text}</button>;
  })}</div>;
}

export function SourcesCited({ citations }: { citations: readonly AgentChatCitation[] }) {
  const [active, setActive] = useState<number | null>(null);
  if (citations.length === 0) return null;
  return <section className="mt-4 min-w-0 rounded-xl border border-sky-200 bg-sky-50/50 p-3 text-xs dark:border-sky-900/60 dark:bg-sky-950/10" aria-label="Sources cited">
    <h3 className="font-semibold">Sources cited</h3>
    <p className="mt-1 text-zinc-600 dark:text-zinc-400">Validated references used in this answer. Citation validation confirms source identity, not sentence-level entailment.</p>
    <ol className="mt-2 space-y-2">{citations.map((citation) => <li key={citation.number}><button id={sourceId(citation)} type="button" onFocus={() => setActive(citation.number)} onBlur={() => setActive(null)} onClick={() => setActive(citation.number)} className={`w-full min-w-0 rounded-lg border p-2 text-left focus-visible:outline-2 focus-visible:outline-sky-600 ${active === citation.number ? 'border-sky-500 bg-white dark:bg-zinc-900' : 'border-zinc-200 bg-white/70 dark:border-zinc-800 dark:bg-zinc-900/70'}`}>
      <span className="block font-semibold">[{citation.number}] <span className="break-all font-mono">{citation.documentId}</span></span>
      <span className="block break-all font-mono text-zinc-600 dark:text-zinc-400">Chunk: {citation.chunkId}</span>
      <span className="block text-zinc-500">Included in model context · Context order {citation.contextOrder + 1}{formatRelevance(citation.normalizedRelevance)}</span>
    </button></li>)}</ol>
  </section>;
}

function sourceId(citation: AgentChatCitation) { return `answer-citation-source-${citation.retrievalCorrelationId}-${citation.number}`; }
function formatRelevance(value: number | undefined) { return typeof value === 'number' && Number.isFinite(value) ? ` · Relevance ${(value * 100).toFixed(1)}%` : ''; }

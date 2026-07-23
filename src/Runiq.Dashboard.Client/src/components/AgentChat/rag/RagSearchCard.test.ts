import assert from 'node:assert/strict';
import test from 'node:test';
import React from 'react';
import TestRenderer, { act } from 'react-test-renderer';

import type { AgentChatRagLifecycle } from '../../../types/agentChat.ts';
import { RagSearchCard } from './RagSearchCard.tsx';

const base = { agentId: 'agent', conversationId: 'conversation', correlationId: 'correlation', indexName: 'docs / ü ? # long-long-long-long-long-long', originalQuery: 'question', requestedCandidateCount: 10 };

// Verifies each blocked readiness renders its developer-facing title, safe details, and encoded keyboard-accessible management link.
test('readiness card renders blocked states and safe navigation', async () => {
  Object.defineProperty(globalThis, 'window', { configurable: true, value: { location: { pathname: '/dashboard/agents/agent/chat/new' } } });
  for (const scenario of [
    { readiness: 'NotInitialized' as const, reason: 'NotInitialized', action: 'StartIngestion' as const, title: 'RAG index is not initialized' },
    { readiness: 'Initializing' as const, reason: 'Initializing', action: 'WaitForIngestion' as const, title: 'RAG index is initializing' },
    { readiness: 'Failed' as const, reason: 'Failed', action: 'RetryIngestion' as const, title: 'RAG index initialization failed' },
    { readiness: undefined, reason: 'IndexNotRegistered', action: 'CheckConfiguration' as const, title: 'RAG index is not registered' },
  ]) {
    const lifecycle: AgentChatRagLifecycle = { status: 'blocked', payload: { ...base, readiness: scenario.readiness, blockingReason: scenario.reason, suggestedAction: scenario.action,
      activeOperationState: scenario.readiness === 'Initializing' ? 'Running' : undefined,
      activeOperationReason: scenario.readiness === 'Initializing' ? 'Manual' : undefined,
      progress: scenario.readiness === 'Initializing' ? { discoveredDocuments: 8, processedDocuments: 3 } : undefined,
      safeFailureSummary: scenario.readiness === 'Failed' ? 'The ingestion operation failed.' : undefined } };
    let renderer!: TestRenderer.ReactTestRenderer;
    await act(async () => { renderer = TestRenderer.create(React.createElement(RagSearchCard, { lifecycle })); });
    await act(async () => { renderer.root.findByType('button').props.onClick(); });
    const text = renderer.root.findAllByType('p').map(node => node.children.join(' ')).join(' ');
    assert.match(JSON.stringify(renderer.toJSON()), new RegExp(scenario.title));
    if (scenario.readiness === 'Initializing') assert.match(text.replace(/\s+/g, ' '), /3 \/ 8 documents/);
    if (scenario.readiness === 'Failed') assert.match(text, /ingestion operation failed/);
    const link = renderer.root.findByType('a');
    assert.equal(link.props.href, `/dashboard/rag?index=${encodeURIComponent(base.indexName)}`);
    assert.match(link.props['aria-label'], /Open RAG Management/);
  }
});

// Verifies degraded readiness remains a completed retrieval card with a warning rather than a failure state.
test('readiness card renders degraded warning inside completed retrieval', async () => {
  const lifecycle: AgentChatRagLifecycle = { status: 'completed', payload: { ...base, actualCandidateCount: 1, acceptedCount: 1, rejectedCount: 0,
    maximumAcceptedResultCount: 1, duration: '00:00:00.0100000', selectedResults: [{ documentId: 'doc', chunkId: 'chunk', contextOrder: 0 }], rejectedResults: [],
    indexReadiness: 'Degraded', safeFailureSummary: 'The ingestion operation failed.' } };
  let renderer!: TestRenderer.ReactTestRenderer;
  await act(async () => { renderer = TestRenderer.create(React.createElement(RagSearchCard, { lifecycle })); });
  await act(async () => { renderer.root.findByType('button').props.onClick(); });
  const rendered = JSON.stringify(renderer.toJSON());
  assert.match(rendered, /RAG index is degraded/);
  assert.match(rendered, /Completed · Degraded/);
  assert.doesNotMatch(rendered, /Vector store query failed/);
});

import assert from 'node:assert/strict';
import test from 'node:test';
import React from 'react';
import TestRenderer, { act } from 'react-test-renderer';
import { GroundingEvidenceCard } from './GroundingEvidenceCard.tsx';
import type { AgentChatRagLifecycle } from '../../../types/agentChat.ts';

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

// Verifies completed retrieval evidence renders only content-free selected and rejected identities after expansion.
test('grounding evidence shows model context sources and rejected candidates', async () => {
  let renderer: TestRenderer.ReactTestRenderer;
  await act(async () => { renderer = TestRenderer.create(React.createElement(GroundingEvidenceCard, { lifecycles: [completed()] })); });
  assert.match(text(renderer!), /Grounded with 1 sources/);
  const button = renderer!.root.findAllByType('button')[0]!;
  await act(async () => button.props.onClick());
  assert.match(text(renderer!), /Included in model context/);
  assert.match(text(renderer!), /document-safe\s+\/\s+chunk-safe/);
  assert.doesNotMatch(text(renderer!), /chunk content|C:\\secret|provider diagnostic/);
  await act(async () => renderer!.root.findAllByType('button')[1]!.props.onClick());
  assert.match(text(renderer!), /Below minimum relevance/);
  await act(async () => renderer!.unmount());
});

// Verifies a completed no-context retrieval is presented as an informational outcome rather than a failure.
test('grounding evidence presents no-context without a synthetic source list', async () => {
  const lifecycle = completed();
  lifecycle.payload.selectedResults = [];
  lifecycle.payload.acceptedCount = 0;
  lifecycle.payload.noContextReason = 'NoResults';
  let renderer: TestRenderer.ReactTestRenderer;
  await act(async () => { renderer = TestRenderer.create(React.createElement(GroundingEvidenceCard, { lifecycles: [lifecycle] })); });
  assert.match(text(renderer!), /No grounding context/);
  await act(async () => renderer!.root.findByType('button').props.onClick());
  assert.match(text(renderer!), /No results/);
  assert.doesNotMatch(text(renderer!), /Grounded with 0 sources/);
  await act(async () => renderer!.unmount());
});

function completed(): Extract<AgentChatRagLifecycle, { status: 'completed' }> { return { status: 'completed', payload: { agentId: 'agent', conversationId: 'conversation', correlationId: 'correlation', indexName: 'corporate-documents', originalQuery: 'question', requestedCandidateCount: 4, actualCandidateCount: 2, acceptedCount: 1, rejectedCount: 1, maximumAcceptedResultCount: 2, duration: '00:00:00.0150000', selectedResults: [{ documentId: 'document-safe', chunkId: 'chunk-safe', contextOrder: 0, rawScore: 0.8, normalizedRelevance: 0.9, metric: 'cosine', higherIsBetter: true }], rejectedResults: [{ documentId: 'document-rejected', chunkId: 'chunk-rejected', rawScore: 0.1, normalizedRelevance: 0.1, reason: 'BelowMinimumRelevance' }] } }; }
function text(renderer: TestRenderer.ReactTestRenderer) { return renderer.root.findAll(() => true).flatMap((node) => node.children.filter((child): child is string => typeof child === 'string')).join(' '); }

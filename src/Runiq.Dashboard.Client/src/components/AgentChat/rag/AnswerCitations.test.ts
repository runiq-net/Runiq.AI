import assert from 'node:assert/strict';
import test from 'node:test';
import React from 'react';
import TestRenderer, { act } from 'react-test-renderer';
import { AnswerWithCitations, SourcesCited } from './AnswerCitations.tsx';
import { splitCitationMarkers } from './citationMarkers.ts';

const citation = { number: 1, documentId: 'remote-work-policy.md', chunkId: 'remote-work-policy.md:chunk:0', retrievalCorrelationId: 'retrieval-1', contextOrder: 0, markerCount: 2 };

test('citation splitting protects links, code, fenced code, and escaped literals', () => {
  const parts = splitCitationMarkers('Valid [1] [1](url) ![1](image) `code [1]` ``code [1]`` \\[1] ```\n[1]\n``` [[1]] [[[1]]] invalid [9]');
  assert.equal(parts.filter((part) => part.number === 1).length, 1);
  assert.equal(parts.filter((part) => part.number === 9).length, 1);
});

test('citation splitting rejects malformed and non-ASCII marker forms', () => {
  const parts = splitCitationMarkers('[ 1 ] [-1] [+1] [١] [999999999999999999999999]');
  assert.equal(parts.some((part) => part.number !== undefined), false);
});

test('only validated markers render as accessible controls', async () => {
  let renderer: TestRenderer.ReactTestRenderer;
  await act(async () => { renderer = TestRenderer.create(React.createElement(AnswerWithCitations, { content: 'Answer [1], invalid [9].', citations: [citation] })); });
  const buttons = renderer!.root.findAllByType('button');
  assert.equal(buttons.length, 1);
  assert.match(buttons[0].props['aria-label'], /remote-work-policy/);
});

test('sources cited lists content-free document and chunk mapping', async () => {
  let renderer: TestRenderer.ReactTestRenderer;
  await act(async () => { renderer = TestRenderer.create(React.createElement(SourcesCited, { citations: [citation] })); });
  const text = JSON.stringify(renderer!.toJSON());
  assert.match(text, /Sources cited/);
  assert.match(text, /remote-work-policy\.md:chunk:0/);
  assert.doesNotMatch(text, /three days/);
});

test('validated marker click focuses its correlation-scoped source row', async () => {
  let focused = false;
  const previousDocument = globalThis.document;
  Object.defineProperty(globalThis, 'document', { configurable: true, value: { getElementById: (id: string) => id === 'answer-citation-source-retrieval-1-1' ? { focus: () => { focused = true; } } : null } });
  try {
    let renderer: TestRenderer.ReactTestRenderer;
    await act(async () => { renderer = TestRenderer.create(React.createElement(AnswerWithCitations, { content: '[1] and [1]', citations: [citation] })); });
    const buttons = renderer!.root.findAllByType('button');
    assert.equal(buttons.length, 2);
    await act(async () => { buttons[1].props.onClick(); });
    assert.equal(focused, true);
  } finally {
    Object.defineProperty(globalThis, 'document', { configurable: true, value: previousDocument });
  }
});

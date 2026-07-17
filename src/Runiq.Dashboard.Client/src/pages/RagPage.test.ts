import assert from 'node:assert/strict';
import test from 'node:test';
import React from 'react';
import TestRenderer, { act, type ReactTestInstance } from 'react-test-renderer';
import { RagPage } from './RagPage.tsx';

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const listeners = new Map<string, EventListener>();
Object.defineProperty(globalThis, 'document', { configurable: true, value: { visibilityState: 'visible', addEventListener: (name: string, listener: EventListener) => listeners.set(name, listener), removeEventListener: (name: string) => listeners.delete(name) } });
Object.defineProperty(globalThis, 'window', { configurable: true, value: globalThis });

// Verifies the real page renders loading, safe list/detail state, selection, scheduled disclosure, and accessible progress semantics.
test('RagPage renders and updates management component state', async () => {
  const pendingList = deferred<Response>();
  const calls: string[] = [];
  globalThis.fetch = async (input) => {
    const url = String(input);
    calls.push(url);
    if (url.endsWith('/api/rag/indexes')) return pendingList.promise;
    if (url.endsWith('/api/rag/indexes/ready-index')) return json(detail('ready-index', 'Ready', 'Manual'));
    if (url.endsWith('/api/rag/indexes/degraded-index')) return json(detail('degraded-index', 'Degraded', 'Scheduled'));
    throw new Error(`Unexpected request: ${url}`);
  };

  let renderer: TestRenderer.ReactTestRenderer;
  await act(async () => { renderer = TestRenderer.create(React.createElement(RagPage)); });
  assert.equal(renderer!.root.findByProps({ 'aria-label': 'Loading registered RAG indexes' }).props['aria-busy'], 'true');

  await act(async () => pendingList.resolve(json([
    listItem('ready-index', 'Ready', 'Manual'),
    listItem('degraded-index', 'Degraded', 'Scheduled'),
    listItem('failed-index-with-an-extremely-long-safe-identifier', 'Failed', 'Manual'),
  ])));
  await flush();

  assert.match(text(renderer!.root), /Registered indexes/);
  assert.match(text(renderer!.root), /Readiness:\s+Ready/);
  assert.match(text(renderer!.root), /Readiness:\s+Failed/);
  assert.match(text(renderer!.root), /Operation:\s+Completed/);
  assert.doesNotMatch(text(renderer!.root), /C:\\secrets|provider stack trace|raw document content/);
  const degraded = button(renderer!.root, 'degraded-index');
  await act(async () => degraded.props.onClick());
  await flush();
  assert.match(text(renderer!.root), /Schedule/);
  assert.match(text(renderer!.root), /0 2 \* \* \*/);
  assert.match(text(renderer!.root), /Safe ingestion failure/);
  assert.doesNotMatch(text(renderer!.root), /provider stack trace/);
  assert.ok(renderer!.root.findAll((node) => node.props.role === 'progressbar').every((node) => node.props['aria-valuenow'] === undefined || typeof node.props['aria-valuenow'] === 'number'));
  assert.ok(calls.some((url) => url.endsWith('/degraded-index')));
  assert.ok(renderer!.root.findAllByType('button').some((item) => item.props['aria-pressed'] !== undefined));
  await act(async () => renderer!.unmount());
});

// Verifies start and cancel buttons call the management API, expose busy disabled state, and refresh status afterward.
test('RagPage executes start and cancel commands through the component boundary', async () => {
  const calls: string[] = [];
  const start = deferred<Response>();
  let statusCalls = 0;
  globalThis.fetch = async (input, init) => {
    const url = String(input);
    calls.push(`${init?.method ?? 'GET'} ${url}`);
    if (url.endsWith('/api/rag/indexes')) return json([listItem('managed-index', 'NotInitialized', 'Manual', null)]);
    if (url.endsWith('/api/rag/indexes/managed-index')) return json(detail('managed-index', 'NotInitialized', 'Manual', null));
    if (url.endsWith('/ingestion/start')) return start.promise;
    if (url.endsWith('/ingestion/cancel')) return json(operation('managed-index', 'Cancelling'));
    if (url.endsWith('/status')) {
      statusCalls += 1;
      return json(runtime('managed-index', statusCalls === 1 ? 'Initializing' : 'Ready', statusCalls === 1 ? operation('managed-index', 'Running') : null));
    }
    throw new Error(`Unexpected request: ${url}`);
  };
  let renderer: TestRenderer.ReactTestRenderer;
  await act(async () => { renderer = TestRenderer.create(React.createElement(RagPage)); });
  await flush();

  const startButton = button(renderer!.root, 'Start ingestion');
  let startAction: Promise<void>;
  await act(async () => { startAction = startButton.props.onClick(); });
  assert.equal(button(renderer!.root, 'Starting…').props.disabled, true);
  await act(async () => start.resolve(json(operation('managed-index', 'Running'))));
  await act(async () => startAction!);
  assert.match(text(renderer!.root), /Operation:\s+Running/);
  assert.ok(calls.some((call) => call.startsWith('POST ') && call.endsWith('/ingestion/start')));
  assert.ok(statusCalls >= 1);

  await act(async () => button(renderer!.root, 'Cancel ingestion').props.onClick());
  assert.ok(calls.some((call) => call.startsWith('POST ') && call.endsWith('/ingestion/cancel')));
  assert.ok(statusCalls >= 2);
  await act(async () => renderer!.unmount());
});

// Verifies a start conflict safely projects the server's active operation instead of crashing the page.
test('RagPage projects a 409 active-operation conflict', async () => {
  globalThis.fetch = async (input, init) => {
    const url = String(input);
    if (url.endsWith('/api/rag/indexes')) return json([listItem('conflict-index', 'NotInitialized', 'Manual', null)]);
    if (url.endsWith('/api/rag/indexes/conflict-index')) return json(detail('conflict-index', 'NotInitialized', 'Manual', null));
    if (url.endsWith('/ingestion/start') && init?.method === 'POST') return json({ code: 'OperationConflict', message: 'An ingestion operation is already active.', activeOperation: operation('conflict-index', 'Running') }, 409);
    throw new Error(`Unexpected request: ${url}`);
  };
  let renderer: TestRenderer.ReactTestRenderer;
  await act(async () => { renderer = TestRenderer.create(React.createElement(RagPage)); });
  await flush();

  await act(async () => button(renderer!.root, 'Start ingestion').props.onClick());

  assert.match(text(renderer!.root), /Operation:\s+Running/);
  assert.match(text(renderer!.root), /already active/);
  await act(async () => renderer!.unmount());
});

// Verifies the no-index component state explains code registration without presenting a create action.
test('RagPage renders the registered-index empty state', async () => {
  globalThis.fetch = async () => json([]);
  let renderer: TestRenderer.ReactTestRenderer;
  await act(async () => { renderer = TestRenderer.create(React.createElement(RagPage)); });
  await flush();
  assert.match(text(renderer!.root), /No RAG indexes are registered/);
  assert.match(text(renderer!.root), /Register an index in application code/);
  assert.doesNotMatch(text(renderer!.root), /Create Index/);
  await act(async () => renderer!.unmount());
});

function listItem(name: string, readiness: string, strategy: string, activeOperation = operation(name, 'Completed')) {
  return { name, sourceCount: 1, sources: [{ identity: 'SAFE-SOURCE', type: 'Directory', displayValue: 'documents/*.md' }], configuration: configuration(strategy), readiness, activeOperation: activeOperation?.state === 'Completed' ? null : activeOperation, lastOperation: activeOperation?.state === 'Completed' ? activeOperation : null };
}

function detail(name: string, readiness: string, strategy: string, activeOperation = operation(name, 'Completed')) {
  const lastFailure = readiness === 'Degraded' ? { code: 'DocumentFailed', message: 'Safe ingestion failure', sourceIdentity: 'SAFE-SOURCE', documentIdentity: 'SAFE-DOCUMENT', timestamp: '2026-07-17T10:00:01Z' } : null;
  return { overview: { name, sourceCount: 1 }, sources: [{ identity: 'SAFE-SOURCE', type: 'Directory', displayValue: 'documents/*.md' }], configuration: configuration(strategy), runtime: { ...runtime(name, readiness, null), lastOperation: activeOperation, progress: activeOperation?.progress ?? null, lastFailure } };
}

function configuration(strategy: string) { return { ingestionStrategy: strategy, scheduleExpression: strategy === 'Scheduled' ? '0 2 * * *' : null, vectorStoreType: 'InMemory', vectorStoreReference: 'in-memory', embeddingReference: 'openai/safe-model', chunkSize: 512, chunkOverlap: 64 }; }
function runtime(indexName: string, readiness: string, activeOperation: ReturnType<typeof operation> | null) { return { indexName, readiness, activeOperation, lastOperation: null, progress: activeOperation?.progress ?? null, lastFailure: null, lastUpdatedAt: '2026-07-17T10:00:01Z' }; }
function operation(indexName: string, state: string) { return { operationId: `operation-${indexName}-very-long-identifier`, indexName, reason: 'Manual', state, startedAt: '2026-07-17T10:00:00Z', completedAt: state === 'Completed' ? '2026-07-17T10:00:01Z' : null, durationMilliseconds: 1000, progress: { discoveredDocuments: 10, processedDocuments: 5, addedDocuments: 3, updatedDocuments: 1, skippedDocuments: 1, deletedDocuments: 0, failedDocuments: 0, producedChunks: 8, producedEmbeddings: 8, currentSourceIdentity: 'SAFE-SOURCE-WITH-A-VERY-LONG-IDENTIFIER', currentDocumentIdentity: 'SAFE-DOCUMENT-WITH-A-VERY-LONG-IDENTIFIER' } }; }
function json(value: unknown, status = 200) { return new Response(JSON.stringify(value), { status, headers: { 'content-type': 'application/json' } }); }
function deferred<T>() { let resolve!: (value: T) => void; const promise = new Promise<T>((complete) => { resolve = complete; }); return { promise, resolve }; }
async function flush() { await act(async () => { await Promise.resolve(); await Promise.resolve(); }); }
function text(node: ReactTestInstance): string { return node.findAll(() => true).flatMap((item) => item.children.filter((child): child is string => typeof child === 'string')).join(' '); }
function button(root: ReactTestInstance, label: string) { return root.findAllByType('button').find((item) => text(item).includes(label)) ?? assert.fail(`Button '${label}' was not found.`); }

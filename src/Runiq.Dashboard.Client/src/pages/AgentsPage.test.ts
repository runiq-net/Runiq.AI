import assert from 'node:assert/strict';
import test from 'node:test';
import React from 'react';
import TestRenderer, { act } from 'react-test-renderer';
import { renderToStaticMarkup } from 'react-dom/server';
import { AgentsPage } from './AgentsPage.tsx';
import { AgentOverviewTab } from '../components/AgentChat/tabs/AgentOverviewTab.tsx';
import type { AgentMetadata } from '../api/agentMetadataApi.ts';

// Verifies both dashboard surfaces render CLI provider metadata and preserve opaque model names.
test('Codex model and provider appear in the list and inspector', async () => {
  const agent: AgentMetadata = {
    id: 'codex', name: 'CodexAgent', provider: 'Codex CLI', model: 'custom/model-name',
    rag: { enabled: false, reranking: { enabled: false, maximumCandidates: 5, timeout: '00:00:05', failurePolicy: 'UseOriginalOrder' } },
  };
  const previousWindow = Object.getOwnPropertyDescriptor(globalThis, 'window');
  const previousFetch = globalThis.fetch;
  let renderer: TestRenderer.ReactTestRenderer | undefined;
  try {
    Object.defineProperty(globalThis, 'window', { configurable: true, value: { __RUNIQ_DASHBOARD__: { basePath: '/dashboard' } } });
    globalThis.fetch = async (input) => {
      assert.equal(input, '/dashboard/metadata/agents');
      return new Response(JSON.stringify([agent]), { headers: { 'Content-Type': 'application/json' } });
    };
    await act(async () => { renderer = TestRenderer.create(React.createElement(AgentsPage)); });
    const list = JSON.stringify(renderer!.toJSON());
    assert.ok(list.includes('Codex CLI'));
    assert.ok(list.includes('custom/model-name'));
    assert.ok(!list.includes('not configured'));
    const inspector = renderToStaticMarkup(React.createElement(AgentOverviewTab, {
      agent, onOpenTools: () => undefined, onOpenTool: () => undefined,
    }));
    assert.ok(inspector.includes('Codex CLI'));
    assert.ok(inspector.includes('custom/model-name'));
  } finally {
    if (renderer) await act(async () => renderer!.unmount());
    globalThis.fetch = previousFetch;
    if (previousWindow) Object.defineProperty(globalThis, 'window', previousWindow);
    else Reflect.deleteProperty(globalThis, 'window');
  }
});

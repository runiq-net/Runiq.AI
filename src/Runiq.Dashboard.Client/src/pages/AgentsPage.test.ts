import assert from 'node:assert/strict';
import test from 'node:test';
import React from 'react';
import TestRenderer, { act } from 'react-test-renderer';
import { renderToStaticMarkup } from 'react-dom/server';
import { AgentsPage } from './AgentsPage.tsx';
import { AgentOverviewTab } from '../components/AgentChat/tabs/AgentOverviewTab.tsx';
import type { AgentMetadata } from '../api/agentMetadataApi.ts';

// Verifies both dashboard surfaces render CLI provider metadata and preserve opaque model names.
for (const scenario of [
  { provider: 'Codex CLI', model: 'custom/model-name', displayModel: 'custom/model-name' },
  { provider: 'Claude CLI', model: null, displayModel: 'CLI default' },
]) {
  // Verifies the list and inspector agree on explicit models and CLI-owned model selection.
  test(`${scenario.provider} model and provider appear in the list and inspector`, async () => {
    const agent: AgentMetadata = {
      id: 'cli-agent', name: 'CliAgent', provider: scenario.provider, model: scenario.model,
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
      assert.ok(list.includes(scenario.provider));
      assert.ok(list.includes(scenario.displayModel));
      assert.ok(!list.includes('not configured'));
      const inspector = renderToStaticMarkup(React.createElement(AgentOverviewTab, {
        agent, onOpenTools: () => undefined, onOpenTool: () => undefined,
      }));
      assert.ok(inspector.includes(scenario.provider));
      assert.ok(inspector.includes(scenario.displayModel));
    } finally {
      if (renderer) await act(async () => renderer!.unmount());
      globalThis.fetch = previousFetch;
      if (previousWindow) Object.defineProperty(globalThis, 'window', previousWindow);
      else Reflect.deleteProperty(globalThis, 'window');
    }
  });
}

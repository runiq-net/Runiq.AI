import assert from 'node:assert/strict';
import test from 'node:test';
import { parseModelReference } from './modelReference.ts';

// Verifies CLI models remain opaque, including names containing a slash.
test('Codex provider metadata preserves the full configured model', () => {
  for (const model of ['gpt-6-sol', 'custom/model-name']) {
    assert.deepEqual(parseModelReference(model, 'Codex CLI'), { provider: 'Codex CLI', model });
  }
});

// Verifies existing model-provider references and missing metadata retain their display behavior.
test('Model-provider parsing remains unchanged without an override', () => {
  assert.deepEqual(parseModelReference('openai/gpt-6-sol'), { provider: 'openai', model: 'gpt-6-sol' });
  assert.deepEqual(parseModelReference(null), { provider: 'Not configured', model: 'Not configured' });
});

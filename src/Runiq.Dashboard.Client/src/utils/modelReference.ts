export type ParsedModelReference = {
  provider: string;
  model: string;
};

export function parseModelReference(
  modelReference: string | null | undefined,
  providerOverride?: string | null,
): ParsedModelReference {
  if (providerOverride) {
    // Claude delegates model selection to the local CLI; an absent model is intentional.
    const fallbackModel = providerOverride === 'Claude CLI' ? 'CLI default' : 'Not configured';
    return { provider: providerOverride, model: modelReference || fallbackModel };
  }
  if (!modelReference) {
    return {
      provider: 'Not configured',
      model: 'Not configured',
    };
  }

  const separatorIndex = modelReference.indexOf('/');

  if (separatorIndex < 0) {
    return {
      provider: 'Unknown',
      model: modelReference,
    };
  }

  const provider = modelReference.slice(0, separatorIndex).trim();
  const model = modelReference.slice(separatorIndex + 1).trim();

  return {
    provider: provider || 'Unknown',
    model: model || 'Not configured',
  };
}

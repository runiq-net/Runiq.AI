export type ParsedModelReference = {
  provider: string;
  model: string;
};

export function parseModelReference(
  modelReference: string | null | undefined,
): ParsedModelReference {
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

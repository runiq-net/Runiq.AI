import type { AgentToolMetadata } from '../../../api/agentMetadataApi';

export function getToolDisplayName(tool: AgentToolMetadata): string {
  const metadataName = tool.displayName?.trim();

  if (metadataName) {
    return metadataName;
  }

  return formatToolName(tool.name);
}

function formatToolName(value: string): string {
  return value
    .replace(/[-_]+/g, ' ')
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    .trim()
    .split(/\s+/)
    .filter(Boolean)
    .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
    .join(' ');
}

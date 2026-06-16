import { getDashboardBasePath } from '../dashboardConfig';
import type { ToolJsonSchema, ToolRunResponse } from './agentMetadataApi';

export type McpToolInfo = {
  name: string;
  description?: string | null;
  source: string;
  hasInput: boolean;
  inputSchema: ToolJsonSchema;
};

export type McpInfo = {
  enabled: boolean;
  endpoint?: string | null;
  fullUrl?: string | null;
  transport: string;
  stateless: boolean;
  authentication: string;
  tools: McpToolInfo[];
};

export async function getMcpInfo(): Promise<McpInfo> {
  const basePath = getDashboardBasePath().replace(/\/+$/g, '');
  const response = await fetch(`${basePath}/api/mcp`);

  if (!response.ok) {
    throw new Error(`Failed to load MCP server info. Status: ${response.status}`);
  }

  return (await response.json()) as McpInfo;
}

export async function runMcpTool(
  basePath: string,
  toolName: string,
  input: Record<string, unknown>,
): Promise<ToolRunResponse> {
  const response = await fetch(
    `${basePath}/api/mcp/tools/${encodeURIComponent(toolName)}/run`,
    {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({
        input,
      }),
    },
  );

  if (!response.ok) {
    throw new Error(`MCP tool run failed with status ${response.status}.`);
  }

  return response.json() as Promise<ToolRunResponse>;
}

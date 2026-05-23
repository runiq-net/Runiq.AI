export type AgentToolMetadata = {
  name: string;
  description?: string;
  inputType?: string;
  outputType?: string;
};

export type AgentMetadata = {
  id: string;
  name: string;
  instructions?: string;
  model?: string;
  reasoningEffort?: string;
  verbosity?: string;
  tools?: AgentToolMetadata[];
};

export type ToolAttachedAgentMetadata = {
  id: string;
  name: string;
};

export type ToolMetadata = {
  name: string;
  displayName: string;
  description?: string;
  typeName: string;
  inputType: string;
  outputType: string;
  hasInput: boolean;
  inputSchema: ToolJsonSchema;
  outputSchema: ToolJsonSchema;
  attachedAgents: ToolAttachedAgentMetadata[];
};

export type ToolJsonSchema = {
  type?: string;
  title?: string;
  properties?: Record<string, ToolJsonSchema>;
  required?: string[];
  enum?: string[];
  items?: ToolJsonSchema;
};

export type ToolRunResponse = {
  isSuccess: boolean;
  outputJson?: string | null;
  errorCode?: string | null;
  errorMessage?: string | null;
};

export async function runTool(
  basePath: string,
  toolName: string,
  input: Record<string, unknown>,
): Promise<ToolRunResponse> {
  const response = await fetch(
    `${basePath}/api/tools/${encodeURIComponent(toolName)}/run`,
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
    throw new Error(`Tool run failed with status ${response.status}.`);
  }

  return response.json() as Promise<ToolRunResponse>;
}

export async function getAgents(basePath: string): Promise<AgentMetadata[]> {
  const response = await fetch(`${basePath}/metadata/agents`);

  if (!response.ok) {
    throw new Error('Agents metadata could not be loaded.');
  }

  return response.json() as Promise<AgentMetadata[]>;
}

export async function getTools(basePath: string): Promise<ToolMetadata[]> {
  const response = await fetch(`${basePath}/metadata/tools`);

  if (!response.ok) {
    throw new Error('Tools metadata could not be loaded.');
  }

  return response.json() as Promise<ToolMetadata[]>;
}
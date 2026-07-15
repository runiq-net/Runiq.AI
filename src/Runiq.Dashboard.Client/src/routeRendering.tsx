import type { DashboardBreadcrumb } from './layouts/DashboardLayout';
import { AgentChatPage } from './pages/AgentChatPage';
import { ToolDetailPage } from './pages/ToolDetailPage';
import { WorkflowDetailPage } from './pages/WorkflowDetailPage';
import { McpPage } from './pages/McpPage';
import { McpToolDetailPage } from './pages/McpToolDetailPage';
import { RagPage } from './pages/RagPage';

import {
  getDashboardRouteByPage,
  type DashboardPage,
  type DashboardRoute,
} from './routes';

export function renderDashboardRoute(route: DashboardRoute) {
  if (route.page === 'agent-chat') {
    return <AgentChatPage agentId={route.agentId} />;
  }

  if (route.page === 'tool-detail') {
    return <ToolDetailPage toolName={route.toolName} />;
  }

  if (route.page === 'mcp') {
    return <McpPage />;
  }

  if (route.page === 'mcp-tool-detail') {
    return <McpToolDetailPage toolName={route.toolName} />;
  }

  if (route.page === 'rag') {
    return <RagPage />;
  }

  if (route.page === 'workflow-detail') {
    return <WorkflowDetailPage workflowId={route.workflowId} />;
  }

  const PageComponent = getDashboardRouteByPage(route.page);

  return <PageComponent.component />;
}

export function getActivePage(route: DashboardRoute): DashboardPage {
  if (route.page === 'agent-chat') {
    return 'agents';
  }

  if (route.page === 'tool-detail') {
    return 'tools';
  }

  if (route.page === 'mcp' || route.page === 'mcp-tool-detail') {
    return 'mcp';
  }

  if (route.page === 'rag') {
    return 'rag';
  }

  if (route.page === 'workflow-detail') {
    return 'workflows';
  }

  return route.page;
}

export function getRouteTitle(route: DashboardRoute): string {
  if (route.page === 'agent-chat') {
    return 'Playground';
  }

  if (route.page === 'tool-detail') {
    return 'Tool Playground';
  }

  if (route.page === 'mcp') {
    return 'MCP Server';
  }

  if (route.page === 'mcp-tool-detail') {
    return 'MCP Tool';
  }

  if (route.page === 'rag') {
    return 'RAG';
  }

  if (route.page === 'workflow-detail') {
    return 'Workflow Execution';
  }

  return getDashboardRouteByPage(route.page).title;
}

export function getRouteSubtitle(route: DashboardRoute): string | undefined {
  if (route.page === 'mcp') {
    return 'Expose selected ASP.NET Core services as MCP tools.';
  }

  if (route.page === 'rag') {
    return 'Inspect read-only retrieval configuration and last-operation telemetry.';
  }

  return undefined;
}

export function getRouteBreadcrumbs(
  route: DashboardRoute,
  navigateTo: (page: DashboardPage) => void,
): DashboardBreadcrumb[] | undefined {
  if (route.page === 'agent-chat') {
    return [
      {
        label: 'Agents',
        onClick: () => navigateTo('agents'),
      },
      {
        label: formatRouteDisplayName(route.agentId),
      },
    ];
  }

  if (route.page === 'tool-detail') {
    return [
      {
        label: 'Tools',
        onClick: () => navigateTo('tools'),
      },
      {
        label: formatRouteDisplayName(route.toolName),
      },
    ];
  }

  if (route.page === 'mcp-tool-detail') {
    return [
      {
        label: 'MCP Server',
        onClick: () => navigateTo('mcp'),
      },
      {
        label: formatRouteDisplayName(route.toolName),
      },
    ];
  }

  if (route.page === 'workflow-detail') {
    return [
      {
        label: 'Workflows',
        onClick: () => navigateTo('workflows'),
      },
      {
        label: formatRouteDisplayName(route.workflowId),
      },
    ];
  }

  return undefined;
}

function formatRouteDisplayName(value: string): string {
  return decodeURIComponent(value)
    .replace(/[-_]+/g, ' ')
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    .trim()
    .split(/\s+/)
    .filter(Boolean)
    .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
    .join(' ');
}

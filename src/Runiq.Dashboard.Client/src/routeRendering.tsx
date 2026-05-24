import type { DashboardBreadcrumb } from './layouts/DashboardLayout';
import { AgentChatPage } from './pages/AgentChatPage';
import { ContextSpaceDetailPage } from './pages/ContextSpaceDetailPage';
import { ToolDetailPage } from './pages/ToolDetailPage';
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

  if (route.page === 'context-space-detail') {
    return <ContextSpaceDetailPage contextSpaceId={route.contextSpaceId} />;
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

  if (route.page === 'context-space-detail') {
    return 'context-spaces';
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

  if (route.page === 'context-space-detail') {
    return 'Context Space';
  }

  return getDashboardRouteByPage(route.page).title;
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

  if (route.page === 'context-space-detail') {
    return [
      {
        label: 'Context Spaces',
        onClick: () => navigateTo('context-spaces'),
      },
      {
        label: formatRouteDisplayName(route.contextSpaceId),
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
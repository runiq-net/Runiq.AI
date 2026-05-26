import type { ComponentType } from 'react';
import { AgentsPage } from './pages/AgentsPage';
import { ToolsPage } from './pages/ToolsPage';
import { ContextSpacesPage } from './pages/ContextSpacesPage';
import { TeamsPage } from './pages/TeamsPage';

export type DashboardPage = 'agents' | 'tools' | 'teams' | 'context-spaces';

export type DashboardRoute =
  | { page: 'agents' }
  | { page: 'tools' }
  | { page: 'teams' }
  | { page: 'agent-chat'; agentId: string }
  | { page: 'team-chat'; teamId: string }
  | { page: 'tool-detail'; toolName: string }
  | { page: 'context-spaces' }
  | { page: 'context-space-detail'; contextSpaceId: string };

export type DashboardRouteDefinition = {
  page: DashboardPage;
  path: string;
  title: string;
  navLabel: string;
  showInNavigation: boolean;
  component: ComponentType;
};

export const dashboardRoutes: DashboardRouteDefinition[] = [
  {
    page: 'agents',
    path: 'agents',
    title: 'Agents',
    navLabel: 'Agents',
    showInNavigation: true,
    component: AgentsPage,
  },
  {
    page: 'tools',
    path: 'tools',
    title: 'Tools',
    navLabel: 'Tools',
    showInNavigation: true,
    component: ToolsPage,
  },
  {
    page: 'teams',
    path: 'teams',
    title: 'Agent Teams',
    navLabel: 'Agent Teams',
    showInNavigation: true,
    component: TeamsPage,
  },
  {
    page: 'context-spaces',
    path: 'context-spaces',
    title: 'Context Spaces',
    navLabel: 'Context Spaces',
    showInNavigation: true,
    component: ContextSpacesPage,
  },
];

export const defaultDashboardPage: DashboardPage = 'agents';

export function getDashboardRouteByPage(
  page: DashboardPage,
): DashboardRouteDefinition {
  return (
    dashboardRoutes.find((route) => route.page === page) ??
    getDefaultDashboardRoute()
  );
}

export function getDefaultDashboardRoute(): DashboardRouteDefinition {
  const defaultRoute = dashboardRoutes.find(
    (route) => route.page === defaultDashboardPage,
  );

  if (!defaultRoute) {
    throw new Error('Default dashboard route is not configured.');
  }

  return defaultRoute;
}

export function resolveDashboardRouteFromPath(
  normalizedPath: string,
): DashboardRouteDefinition {
  const route = dashboardRoutes.find(
    (item) => item.path.toLowerCase() === normalizedPath.toLowerCase(),
  );

  return route ?? getDefaultDashboardRoute();
}

export function resolveDashboardRouteFromUrl(
  pathname: string,
  basePath: string,
): DashboardRoute {
  const relativePath = removeBasePath(pathname, basePath);

  const segments = relativePath
    .replace(/^\/+|\/+$/g, '')
    .split('/')
    .map((segment) => segment.trim())
    .filter(Boolean);

  if (segments.length === 0) {
    return { page: defaultDashboardPage };
  }

  const firstSegment = segments[0].toLowerCase();

  if (
    firstSegment === 'agents' &&
    segments.length === 4 &&
    segments[2].toLowerCase() === 'chat' &&
    segments[3].toLowerCase() === 'new'
  ) {
    return {
      page: 'agent-chat',
      agentId: decodeURIComponent(segments[1]),
    };
  }

  if (
    firstSegment === 'teams' &&
    segments.length === 4 &&
    segments[2].toLowerCase() === 'chat' &&
    segments[3].toLowerCase() === 'new'
  ) {
    return {
      page: 'team-chat',
      teamId: decodeURIComponent(segments[1]),
    };
  }

  if (firstSegment === 'tools' && segments.length === 2) {
    return {
      page: 'tool-detail',
      toolName: decodeURIComponent(segments[1]),
    };
  }

  if (firstSegment === 'agents') {
    return { page: 'agents' };
  }

  if (firstSegment === 'tools') {
    return { page: 'tools' };
  }

  if (firstSegment === 'teams') {
    return { page: 'teams' };
  }

  if (firstSegment === 'context-spaces') {
    if (segments[1]) {
      return {
        page: 'context-space-detail',
        contextSpaceId: decodeURIComponent(segments[1]),
      };
    }

    return { page: 'context-spaces' };
  }



  return { page: defaultDashboardPage };
}

export function getNavigationRoutes(): DashboardRouteDefinition[] {
  return dashboardRoutes.filter((route) => route.showInNavigation);
}

function removeBasePath(pathname: string, basePath: string): string {
  const normalizedBasePath = basePath.replace(/\/+$/g, '');

  if (!normalizedBasePath) {
    return pathname;
  }

  const lowerPathname = pathname.toLowerCase();
  const lowerBasePath = normalizedBasePath.toLowerCase();

  if (lowerPathname === lowerBasePath) {
    return '';
  }

  if (lowerPathname.startsWith(`${lowerBasePath}/`)) {
    return pathname.slice(normalizedBasePath.length);
  }

  return pathname;
}
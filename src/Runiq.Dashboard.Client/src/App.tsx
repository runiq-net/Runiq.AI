import { useEffect, useMemo, useState } from 'react';

import { getDashboardBasePath, getDashboardTitle } from './dashboardConfig';
import { DashboardLayout, type DashboardBreadcrumb } from './layouts/DashboardLayout';
import { AgentChatPage } from './pages/AgentChatPage';
import { ToolDetailPage } from './pages/ToolDetailPage';
import {
  getDashboardRouteByPage,
  getNavigationRoutes,
  resolveDashboardRouteFromUrl,
  type DashboardPage,
  type DashboardRoute,
} from './routes';

function App() {
  const basePath = useMemo(() => getDashboardBasePath(), []);
  const dashboardTitle = useMemo(() => getDashboardTitle(), []);
  const navigationRoutes = useMemo(() => getNavigationRoutes(), []);

  const [route, setRoute] = useState<DashboardRoute>(() =>
    resolveDashboardRouteFromUrl(window.location.pathname, basePath),
  );

  useEffect(() => {
    const handlePopState = () => {
      setRoute(resolveDashboardRouteFromUrl(window.location.pathname, basePath));
    };

    window.addEventListener('popstate', handlePopState);

    return () => {
      window.removeEventListener('popstate', handlePopState);
    };
  }, [basePath]);

  const navigateTo = (page: DashboardPage) => {
    const targetDefinition = getDashboardRouteByPage(page);
    const targetPath = buildDashboardPath(basePath, targetDefinition.path);

    window.history.pushState({}, '', targetPath);
    setRoute({ page: targetDefinition.page });
  };

  const activePage = getActivePage(route);
  const title = getRouteTitle(route);
  const breadcrumbs = getRouteBreadcrumbs(route, navigateTo);

  return (
    <DashboardLayout
      title={title}
      breadcrumbs={breadcrumbs}
      activePage={activePage}
      dashboardTitle={dashboardTitle}
      navigationRoutes={navigationRoutes}
      onNavigate={navigateTo}
    >
      {renderRoute(route)}
    </DashboardLayout>
  );
}

function renderRoute(route: DashboardRoute) {
  if (route.page === 'agent-chat') {
    return <AgentChatPage agentId={route.agentId} />;
  }

  if (route.page === 'tool-detail') {
    return <ToolDetailPage toolName={route.toolName} />;
  }

  const PageComponent = getDashboardRouteByPage(route.page).component;

  return <PageComponent />;
}

function getActivePage(route: DashboardRoute): DashboardPage {
  if (route.page === 'agent-chat') {
    return 'agents';
  }

  if (route.page === 'tool-detail') {
    return 'tools';
  }

  return route.page;
}

function getRouteTitle(route: DashboardRoute): string {
  if (route.page === 'agent-chat') {
    return 'Playground';
  }

  if (route.page === 'tool-detail') {
    return 'Tool Playground';
  }

  return getDashboardRouteByPage(route.page).title;
}

function getRouteBreadcrumbs(
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

function buildDashboardPath(basePath: string, relativePath: string): string {
  const normalizedBasePath = basePath.replace(/\/+$/g, '');
  const normalizedRelativePath = relativePath.replace(/^\/+/g, '');

  return `${normalizedBasePath}/${normalizedRelativePath}`;
}

export default App;
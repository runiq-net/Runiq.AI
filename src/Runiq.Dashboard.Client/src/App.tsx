import { useEffect, useMemo, useState } from 'react';

import { getDashboardBasePath, getDashboardTitle } from './dashboardConfig';
import { DashboardLayout } from './layouts/DashboardLayout';
import {
  getActivePage,
  getRouteBreadcrumbs,
  getRouteTitle,
  renderDashboardRoute,
} from './routeRendering';
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

  return (
    <DashboardLayout
      title={getRouteTitle(route)}
      breadcrumbs={getRouteBreadcrumbs(route, navigateTo)}
      activePage={getActivePage(route)}
      dashboardTitle={dashboardTitle}
      navigationRoutes={navigationRoutes}
      onNavigate={navigateTo}
    >
      {renderDashboardRoute(route)}
    </DashboardLayout>
  );
}

function buildDashboardPath(basePath: string, relativePath: string): string {
  const normalizedBasePath = basePath.replace(/\/+$/g, '');
  const normalizedRelativePath = relativePath.replace(/^\/+/g, '');

  return `${normalizedBasePath}/${normalizedRelativePath}`;
}

export default App;
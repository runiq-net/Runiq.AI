declare global {
  interface Window {
    __RUNIQ_DASHBOARD__?: {
      basePath?: string;
      title?: string;
      authentication?: {
        accessMode?: string;
        logoutPath?: string;
      };
    };
  }
}

export function getDashboardBasePath(): string {
  const configuredBasePath = window.__RUNIQ_DASHBOARD__?.basePath ?? '/runiq';

  if (configuredBasePath === '/') {
    return '';
  }

  return configuredBasePath.replace(/\/+$/, '');
}

export function getDashboardTitle(): string {
  return window.__RUNIQ_DASHBOARD__?.title ?? 'Runiq Studio';
}

export function shouldShowDashboardLogout(): boolean {
  const accessMode = window.__RUNIQ_DASHBOARD__?.authentication?.accessMode;

  return accessMode === 'AuthenticatedUser' || accessMode === 'Role';
}

export function getDashboardLogoutPath(): string {
  return window.__RUNIQ_DASHBOARD__?.authentication?.logoutPath ?? '/logout';
}

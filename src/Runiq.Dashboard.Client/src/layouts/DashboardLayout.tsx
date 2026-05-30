import { PanelLeftClose, PanelLeftOpen } from 'lucide-react';
import { useState, type ReactNode } from 'react';

import { SidebarItem } from '../components/Sidebar/SidebarItem';
import { ThemeToggle } from '../components/ThemeToggle/ThemeToggle';
import type { DashboardPage, DashboardRouteDefinition } from '../routes';
import runiqLogoDark from '../../../../assets/runiq-logo-dark.png';
import runiqLogoLight from '../../../../assets/runiq-logo-light.png';

export type DashboardBreadcrumb = {
  label: string;
  onClick?: () => void;
};

type DashboardLayoutProps = {
  title: string;
  breadcrumbs?: DashboardBreadcrumb[];
  activePage: DashboardPage;
  dashboardTitle: string;
  navigationRoutes: DashboardRouteDefinition[];
  children: ReactNode;
  onNavigate: (page: DashboardPage) => void;
};

export function DashboardLayout({
  title,
  breadcrumbs,
  activePage,
  navigationRoutes,
  children,
  onNavigate,
}: DashboardLayoutProps) {
  const [isSidebarCollapsed, setSidebarCollapsed] = useState(false);
  const [isMobileSidebarOpen, setMobileSidebarOpen] = useState(false);

  const closeMobileSidebar = () => {
    setMobileSidebarOpen(false);
  };

  const navigateTo = (page: DashboardPage) => {
    onNavigate(page);
    closeMobileSidebar();
  };

  return (
    <main className="flex h-dvh w-full overflow-hidden bg-zinc-50 text-zinc-950 dark:bg-[#050505] dark:text-zinc-100">
      {isMobileSidebarOpen && (
        <button
          type="button"
          aria-label="Close navigation backdrop"
          className="fixed inset-0 z-30 bg-black/60 backdrop-blur-sm lg:hidden"
          onClick={closeMobileSidebar}
        />
      )}

      <aside
        className={[
          'fixed inset-y-0 left-0 z-40 flex h-dvh flex-col border-r border-zinc-200 bg-white p-5 shadow-2xl transition-all duration-200 dark:border-zinc-800 dark:bg-[#070707] lg:static lg:z-auto lg:shadow-none',
          isMobileSidebarOpen
            ? 'translate-x-0'
            : '-translate-x-full lg:translate-x-0',
          isSidebarCollapsed
            ? 'lg:w-20 lg:px-3'
            : 'w-[320px] max-w-[calc(100vw-48px)] lg:w-[270px]',
        ].join(' ')}
      >
        <div
          className={[
            'flex min-h-10 items-center gap-3',
            isSidebarCollapsed ? 'lg:justify-center' : 'justify-between',
          ].join(' ')}
        >
          <div
            className={[
              'flex min-w-0 items-center gap-3',
              isSidebarCollapsed ? 'lg:hidden' : '',
            ].join(' ')}
          >
            <img
              src={runiqLogoLight}
              alt="RunIQ"
              className="h-9 w-auto max-w-[150px] object-contain dark:hidden"
            />
            <img
              src={runiqLogoDark}
              alt="RunIQ"
              className="hidden h-9 w-auto max-w-[150px] object-contain dark:block"
            />
          </div>

          <button
            type="button"
            className="inline-flex size-9 shrink-0 items-center justify-center rounded-xl border border-zinc-200 bg-white text-zinc-700 transition hover:border-zinc-300 hover:bg-zinc-100 dark:border-zinc-800 dark:bg-zinc-950 dark:text-zinc-100 dark:hover:border-zinc-700 dark:hover:bg-zinc-900"
            aria-label={
              isMobileSidebarOpen
                ? 'Close navigation'
                : isSidebarCollapsed
                  ? 'Expand navigation'
                  : 'Collapse navigation'
            }
            aria-expanded={isMobileSidebarOpen ? true : !isSidebarCollapsed}
            onClick={() => {
              if (isMobileSidebarOpen) {
                closeMobileSidebar();
                return;
              }

              setSidebarCollapsed((value) => !value);
            }}
          >
            {isSidebarCollapsed && !isMobileSidebarOpen ? (
              <PanelLeftOpen size={18} strokeWidth={2} aria-hidden="true" />
            ) : (
              <PanelLeftClose size={18} strokeWidth={2} aria-hidden="true" />
            )}
          </button>
        </div>

        <div className={isSidebarCollapsed ? 'mt-9 lg:mt-10' : 'mt-9'}>
          <div
            className={[
              'mb-3 flex items-center gap-3 px-1 text-[11px] font-medium uppercase tracking-[0.18em] text-zinc-500 dark:text-zinc-500',
              isSidebarCollapsed ? 'lg:hidden' : '',
            ].join(' ')}
          >
            <span>Primitives</span>
            <div className="h-px flex-1 bg-zinc-200 dark:bg-zinc-800" />
          </div>

          <nav className="flex flex-col gap-1">
            {navigationRoutes.map((route) => (
              <SidebarItem
                key={route.page}
                label={route.navLabel}
                active={activePage === route.page}
                collapsed={isSidebarCollapsed}
                onClick={() => navigateTo(route.page)}
              />
            ))}
          </nav>
        </div>

        <div
          className={[
            'mt-auto border-t border-zinc-200 pt-4 dark:border-zinc-800',
            isSidebarCollapsed ? 'lg:hidden' : '',
          ].join(' ')}
        >
          <div className="text-[10px] font-semibold uppercase tracking-[0.14em] text-zinc-500">
            Runiq Version
          </div>

          <div className="mt-1.5 font-mono text-xs text-emerald-400">
            0.1.0
          </div>
        </div>
      </aside>

      <section className="flex min-w-0 flex-1 flex-col">
<header className="flex h-[88px] shrink-0 items-center justify-between gap-4 border-b border-zinc-200 bg-white px-6 dark:border-zinc-800 dark:bg-[#050505] lg:px-10">
  <div className="flex min-w-0 items-center gap-4">
    <button
      type="button"
      className="inline-flex size-10 items-center justify-center rounded-xl border border-zinc-200 bg-white text-zinc-700 transition hover:border-zinc-300 hover:bg-zinc-100 dark:border-zinc-800 dark:bg-zinc-950 dark:text-zinc-100 dark:hover:border-zinc-700 dark:hover:bg-zinc-900 lg:hidden"
      aria-label="Open navigation"
      aria-expanded={isMobileSidebarOpen}
      onClick={() => setMobileSidebarOpen(true)}
    >
      <PanelLeftOpen size={19} strokeWidth={2} aria-hidden="true" />
    </button>

    <div className="min-w-0">
      <h1 className="m-0 truncate text-[22px] font-bold leading-7 tracking-tight text-zinc-950 dark:text-zinc-100">
        {title}
      </h1>

      <nav
        aria-label="Breadcrumb"
        className="mt-1 flex h-5 min-w-0 items-center gap-1 text-sm text-zinc-500 dark:text-zinc-500"
      >
        {breadcrumbs && breadcrumbs.length > 0 ? (
          breadcrumbs.map((breadcrumb, index) => {
            const isLast = index === breadcrumbs.length - 1;

            return (
              <span
                key={`${breadcrumb.label}-${index}`}
                className="flex min-w-0 items-center gap-1"
              >
                {breadcrumb.onClick && !isLast ? (
                  <button
                    type="button"
                    onClick={breadcrumb.onClick}
                    className="truncate font-medium text-zinc-500 transition hover:text-zinc-950 dark:text-zinc-500 dark:hover:text-zinc-100"
                  >
                    {breadcrumb.label}
                  </button>
                ) : (
                  <span
                    className={[
                      'truncate',
                      isLast ? 'text-zinc-700 dark:text-zinc-300' : '',
                    ].join(' ')}
                  >
                    {breadcrumb.label}
                  </span>
                )}

                {!isLast && (
                  <span className="shrink-0 text-zinc-400 dark:text-zinc-600">
                    /
                  </span>
                )}
              </span>
            );
          })
        ) : (
          <span aria-hidden="true">&nbsp;</span>
        )}
      </nav>
    </div>
  </div>

  <ThemeToggle />
</header>

        <div className="min-h-0 flex-1 overflow-auto p-6 lg:p-10">
          {children}
        </div>
      </section>
    </main>
  );
}

import type { ComponentType } from 'react';

type SidebarItemProps = {
  label: string;
  active: boolean;
  collapsed: boolean;
  icon: ComponentType<{ className?: string; 'aria-hidden'?: boolean }>;
  onClick: () => void;
};

export function SidebarItem({
  label,
  active,
  collapsed,
  icon: Icon,
  onClick,
}: SidebarItemProps) {
  return (
    <button
      type="button"
      title={label}
      onClick={onClick}
      className={[
        'flex items-center gap-3 rounded-lg px-3 py-2.5 text-left text-sm transition',
        collapsed ? 'justify-center px-0' : '',
        active
          ? 'bg-emerald-500/10 text-emerald-600 ring-1 ring-emerald-500/30 dark:bg-emerald-400/10 dark:text-emerald-300 dark:ring-emerald-400/30'
          : 'text-zinc-500 hover:bg-zinc-100 hover:text-zinc-950 dark:text-zinc-400 dark:hover:bg-zinc-900/70 dark:hover:text-zinc-100',
      ].join(' ')}
    >
      <Icon className="size-4 shrink-0" aria-hidden />

      <span className={collapsed ? 'hidden' : ''}>{label}</span>
    </button>
  );
}

import { useEffect, useState, type ReactNode } from 'react';
import {
  AlertTriangle,
  CheckCircle2,
  Clock3,
  Database,
  Gauge,
  Hash,
  Layers3,
  Search,
  UploadCloud,
} from 'lucide-react';

import { getRagInfo, type RagInfo } from '../api/ragApi';

export function RagPage() {
  const [info, setInfo] = useState<RagInfo | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setLoading] = useState(true);

  useEffect(() => {
    let isMounted = true;

    getRagInfo()
      .then((result) => {
        if (!isMounted) {
          return;
        }

        setInfo(result);
        setError(null);
      })
      .catch((exception: unknown) => {
        if (!isMounted) {
          return;
        }

        setError(
          exception instanceof Error ? exception.message : 'Failed to load RAG info.',
        );
      })
      .finally(() => {
        if (isMounted) {
          setLoading(false);
        }
      });

    return () => {
      isMounted = false;
    };
  }, []);

  if (isLoading) {
    return (
      <StatePanel>
        Loading RAG info...
      </StatePanel>
    );
  }

  if (error) {
    return (
      <StatePanel tone="error">
        {error}
      </StatePanel>
    );
  }

  if (!info) {
    return null;
  }

  return (
    <div className="grid gap-5 xl:grid-cols-[minmax(0,1fr)_360px]">
      <div className="min-w-0 space-y-5">
        {info.diagnostics ? (
          <section className="rounded-xl border border-amber-200 bg-amber-50 p-5 text-sm text-amber-800 shadow-sm dark:border-amber-900/60 dark:bg-amber-950/30 dark:text-amber-200">
            <div className="flex items-start gap-3">
              <AlertTriangle className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
              <span className="min-w-0 break-words">{info.diagnostics}</span>
            </div>
          </section>
        ) : null}

        <section className="rounded-xl border border-zinc-200 bg-white p-5 shadow-sm dark:border-zinc-800 dark:bg-zinc-950">
          <div className="mb-4 flex items-center justify-between gap-3">
            <h3 className="m-0 text-lg font-semibold text-zinc-950 dark:text-zinc-100">
              Configuration
            </h3>
            <StatusPill enabled={info.enabled} />
          </div>

          <div className="grid gap-3 md:grid-cols-2">
            <MetricTile
              icon={<Database className="size-4" aria-hidden="true" />}
              label="Vector Store"
              value={formatValue(info.vectorStore)}
              mono
            />
            <MetricTile
              icon={<Hash className="size-4" aria-hidden="true" />}
              label="Index Name"
              value={formatValue(info.indexName)}
              mono
            />
            <MetricTile
              icon={<Search className="size-4" aria-hidden="true" />}
              label="Default Top K"
              value={formatNumber(info.defaultTopK)}
            />
            <MetricTile
              icon={<Gauge className="size-4" aria-hidden="true" />}
              label="Embedding Dimension"
              value={formatNumber(info.embeddingDimension)}
            />
          </div>
        </section>

        <section className="rounded-xl border border-zinc-200 bg-white p-5 shadow-sm dark:border-zinc-800 dark:bg-zinc-950">
          <div className="mb-4 flex items-center justify-between gap-3">
            <h3 className="m-0 text-lg font-semibold text-zinc-950 dark:text-zinc-100">
              Last Operations
            </h3>
          </div>

          <div className="grid gap-4 lg:grid-cols-2">
            <OperationPanel
              title="Upsert"
              icon={<UploadCloud className="size-4" aria-hidden="true" />}
              isAvailable={Boolean(info.lastUpsert)}
              succeeded={info.lastUpsert?.succeeded}
              errorCode={info.lastUpsert?.errorCode}
              reason={info.lastUpsert?.reason}
              timestamp={info.lastUpsert?.timestamp}
              rows={[
                {
                  label: 'Chunks',
                  value: formatNumber(info.lastUpsert?.chunkCount),
                },
              ]}
            />

            <OperationPanel
              title="Retrieval"
              icon={<Search className="size-4" aria-hidden="true" />}
              isAvailable={Boolean(info.lastRetrieval)}
              succeeded={info.lastRetrieval?.succeeded}
              errorCode={info.lastRetrieval?.errorCode}
              reason={info.lastRetrieval?.reason}
              timestamp={info.lastRetrieval?.timestamp}
              rows={[
                {
                  label: 'Duration',
                  value: formatDuration(info.lastRetrieval?.durationMilliseconds),
                },
                {
                  label: 'Results',
                  value: formatNumber(info.lastRetrieval?.resultCount),
                },
              ]}
            />
          </div>
        </section>
      </div>

      <aside className="min-w-0 rounded-xl border border-zinc-200 bg-white p-4 shadow-sm dark:border-zinc-800 dark:bg-zinc-950">
        <div className="space-y-2">
          <InspectorCard title="Status">
            <StatusCard enabled={info.enabled} />
          </InspectorCard>

          <InspectorCard title="Store">
            <KeyValueRow
              icon={<Database className="size-3.5" aria-hidden="true" />}
              label="Provider"
              value={formatValue(info.vectorStore)}
              mono
            />
            <KeyValueRow
              icon={<Hash className="size-3.5" aria-hidden="true" />}
              label="Index"
              value={formatValue(info.indexName)}
              mono
            />
          </InspectorCard>

          <InspectorCard title="Retrieval">
            <KeyValueRow
              icon={<Search className="size-3.5" aria-hidden="true" />}
              label="Default Top K"
              value={formatNumber(info.defaultTopK)}
            />
            <KeyValueRow
              icon={<Clock3 className="size-3.5" aria-hidden="true" />}
              label="Last Duration"
              value={formatDuration(info.lastRetrieval?.durationMilliseconds)}
            />
          </InspectorCard>

          <InspectorCard title="Chunks">
            <KeyValueRow
              icon={<Layers3 className="size-3.5" aria-hidden="true" />}
              label="Last Upsert"
              value={formatNumber(info.lastUpsert?.chunkCount)}
            />
          </InspectorCard>
        </div>
      </aside>
    </div>
  );
}

type OperationRow = {
  label: string;
  value: string;
};

function OperationPanel({
  title,
  icon,
  isAvailable,
  succeeded,
  errorCode,
  reason,
  timestamp,
  rows,
}: {
  title: string;
  icon: ReactNode;
  isAvailable: boolean;
  succeeded?: boolean;
  errorCode?: string | null;
  reason?: string | null;
  timestamp?: string | null;
  rows: OperationRow[];
}) {
  return (
    <div className="rounded-lg border border-zinc-200 bg-zinc-50 p-4 dark:border-zinc-800 dark:bg-zinc-900/40">
      <div className="mb-4 flex items-center justify-between gap-3">
        <div className="flex min-w-0 items-center gap-2">
          <span className="inline-flex size-8 shrink-0 items-center justify-center rounded-lg bg-zinc-100 text-zinc-600 dark:bg-zinc-800 dark:text-zinc-300">
            {icon}
          </span>
          <h4 className="m-0 truncate text-sm font-semibold text-zinc-950 dark:text-zinc-100">
            {title}
          </h4>
        </div>
        <OperationStatusPill isAvailable={isAvailable} succeeded={succeeded} />
      </div>

      {!isAvailable ? (
        <div className="rounded-md border border-dashed border-zinc-300 p-4 text-sm text-zinc-500 dark:border-zinc-700 dark:text-zinc-500">
          No operation recorded yet.
        </div>
      ) : (
        <div className="space-y-3">
          <div className="grid gap-2 sm:grid-cols-2">
            {rows.map((row) => (
              <div key={row.label} className="rounded-md bg-white p-3 dark:bg-zinc-950">
                <div className="text-xs font-medium uppercase text-zinc-500 dark:text-zinc-500">
                  {row.label}
                </div>
                <div className="mt-1 break-words text-sm font-semibold text-zinc-950 dark:text-zinc-100">
                  {row.value}
                </div>
              </div>
            ))}
          </div>

          <KeyValueRow
            icon={<AlertTriangle className="size-3.5" aria-hidden="true" />}
            label="Error Code"
            value={formatValue(errorCode)}
          />
          <KeyValueRow
            icon={<Clock3 className="size-3.5" aria-hidden="true" />}
            label="Recorded"
            value={formatTimestamp(timestamp)}
          />

          {reason ? (
            <div className="rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-700 dark:border-red-900/60 dark:bg-red-950/30 dark:text-red-300">
              {reason}
            </div>
          ) : null}
        </div>
      )}
    </div>
  );
}

function MetricTile({
  icon,
  label,
  value,
  mono = false,
}: {
  icon: ReactNode;
  label: string;
  value: string;
  mono?: boolean;
}) {
  return (
    <div className="rounded-lg border border-zinc-200 bg-zinc-50 p-4 dark:border-zinc-800 dark:bg-zinc-900/40">
      <div className="flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.14em] text-zinc-500 dark:text-zinc-500">
        <span className="shrink-0">{icon}</span>
        <span className="truncate">{label}</span>
      </div>
      <div
        className={[
          'mt-3 min-w-0 break-words text-lg font-semibold text-zinc-950 dark:text-zinc-100',
          mono ? 'font-mono text-base' : '',
        ].join(' ')}
      >
        {value}
      </div>
    </div>
  );
}

function StatusPill({ enabled }: { enabled: boolean }) {
  return (
    <span
      className={[
        'rounded-full px-2.5 py-1 text-xs font-semibold',
        enabled
          ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950/50 dark:text-emerald-300'
          : 'bg-zinc-100 text-zinc-600 dark:bg-zinc-900 dark:text-zinc-400',
      ].join(' ')}
    >
      {enabled ? 'Enabled' : 'Not registered'}
    </span>
  );
}

function OperationStatusPill({
  isAvailable,
  succeeded,
}: {
  isAvailable: boolean;
  succeeded?: boolean;
}) {
  if (!isAvailable) {
    return (
      <span className="rounded-full bg-zinc-100 px-2.5 py-1 text-xs font-semibold text-zinc-600 dark:bg-zinc-900 dark:text-zinc-400">
        Not available
      </span>
    );
  }

  return (
    <span
      className={[
        'rounded-full px-2.5 py-1 text-xs font-semibold',
        succeeded
          ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950/50 dark:text-emerald-300'
          : 'bg-red-100 text-red-700 dark:bg-red-950/40 dark:text-red-300',
      ].join(' ')}
    >
      {succeeded ? 'Succeeded' : 'Failed'}
    </span>
  );
}

function StatusCard({ enabled }: { enabled: boolean }) {
  return (
    <div
      className={[
        'flex items-center justify-between gap-3 rounded-md px-2.5 py-2 text-sm font-semibold',
        enabled
          ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950/50 dark:text-emerald-300'
          : 'bg-zinc-100 text-zinc-600 dark:bg-zinc-900 dark:text-zinc-400',
      ].join(' ')}
    >
      <span>{enabled ? 'Enabled' : 'Not registered'}</span>
      {enabled ? (
        <CheckCircle2 className="size-4" aria-hidden="true" />
      ) : (
        <span className="size-2.5 rounded-full bg-zinc-400" aria-hidden="true" />
      )}
    </div>
  );
}

function InspectorCard({
  title,
  children,
}: {
  title: string;
  children: ReactNode;
}) {
  return (
    <section className="rounded-md border border-zinc-200 bg-zinc-50 p-3 dark:border-zinc-800 dark:bg-zinc-900/40">
      <div className="text-xs font-semibold uppercase tracking-[0.14em] text-zinc-500 dark:text-zinc-500">
        {title}
      </div>

      <div className="mt-2 space-y-1.5 text-sm">{children}</div>
    </section>
  );
}

function KeyValueRow({
  icon,
  label,
  value,
  mono = false,
}: {
  icon: ReactNode;
  label: string;
  value: string;
  mono?: boolean;
}) {
  return (
    <div className="flex items-center justify-between gap-4">
      <span className="flex min-w-0 items-center gap-1.5 text-zinc-500 dark:text-zinc-500">
        <span className="shrink-0 text-zinc-500 dark:text-zinc-500">{icon}</span>
        <span className="truncate">{label}</span>
      </span>
      <span
        className={[
          'min-w-0 truncate text-right font-medium text-zinc-800 dark:text-zinc-200',
          mono ? 'font-mono' : '',
        ].join(' ')}
      >
        {value}
      </span>
    </div>
  );
}

function StatePanel({
  tone = 'neutral',
  children,
}: {
  tone?: 'neutral' | 'error';
  children: ReactNode;
}) {
  return (
    <div
      className={[
        'rounded-xl border p-5 text-sm shadow-sm',
        tone === 'error'
          ? 'border-red-200 bg-red-50 text-red-700 dark:border-red-900/60 dark:bg-red-950/30 dark:text-red-300'
          : 'border-zinc-200 bg-white text-zinc-600 dark:border-zinc-800 dark:bg-zinc-950 dark:text-zinc-400',
      ].join(' ')}
    >
      {children}
    </div>
  );
}

function formatValue(value: string | null | undefined): string {
  const normalized = value?.trim();

  return normalized ? normalized : 'Not available';
}

function formatNumber(value: number | null | undefined): string {
  return typeof value === 'number' && Number.isFinite(value)
    ? value.toLocaleString()
    : 'Not available';
}

function formatDuration(value: number | null | undefined): string {
  if (typeof value !== 'number' || !Number.isFinite(value)) {
    return 'Not available';
  }

  return `${value.toLocaleString(undefined, {
    maximumFractionDigits: 2,
  })} ms`;
}

function formatTimestamp(value: string | null | undefined): string {
  if (!value) {
    return 'Not available';
  }

  const parsed = new Date(value);

  if (Number.isNaN(parsed.getTime())) {
    return value;
  }

  return parsed.toLocaleString();
}

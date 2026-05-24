import { Copy } from 'lucide-react';

type ToolJsonBlockProps = {
  title: string;
  value?: string;
  emptyText: string;
  isError?: boolean;
};

export function ToolJsonBlock({
  title,
  value,
  emptyText,
  isError = false,
}: ToolJsonBlockProps) {
  const displayValue = value ? formatJson(value) : emptyText;

  async function handleCopy() {
    try {
      await navigator.clipboard.writeText(displayValue);
    } catch {
      // Copy failure should not break the chat UI.
    }
  }

  return (
    <div>
      <div className="mb-2 flex items-center justify-between gap-3">
        <div className="text-sm font-semibold text-zinc-950 dark:text-zinc-100">
          {title}
        </div>

        <button
          type="button"
          onClick={handleCopy}
          className="inline-flex size-8 items-center justify-center rounded-lg border border-zinc-200 text-zinc-500 transition hover:bg-zinc-100 hover:text-zinc-900 dark:border-zinc-800 dark:text-zinc-400 dark:hover:bg-zinc-900 dark:hover:text-zinc-100"
          aria-label={`Copy ${title}`}
          title="Copy"
        >
          <Copy className="size-4" />
        </button>
      </div>

      <pre
        className={[
          'max-h-72 overflow-auto rounded-xl border p-3 text-xs leading-6',
          'whitespace-pre-wrap break-words',
          isError
            ? 'border-red-200 bg-red-50 text-red-700 dark:border-red-900/60 dark:bg-red-950/20 dark:text-red-300'
            : 'border-zinc-200 bg-zinc-50 text-zinc-800 dark:border-zinc-800 dark:bg-zinc-900/80 dark:text-zinc-300',
        ].join(' ')}
      >
        {displayValue}
      </pre>
    </div>
  );
}

function formatJson(value: string): string {
  try {
    return JSON.stringify(JSON.parse(value), null, 2);
  } catch {
    return value;
  }
}

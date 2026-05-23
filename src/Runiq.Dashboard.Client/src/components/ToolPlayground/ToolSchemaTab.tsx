import { Check, Copy, FileJson } from 'lucide-react';
import { useMemo, useState, type ReactNode } from 'react';

import type { ToolJsonSchema, ToolMetadata } from '../../api/agentMetadataApi';

type ToolSchemaTabProps = {
  tool: ToolMetadata;
};

export function ToolSchemaTab({ tool }: ToolSchemaTabProps) {
  return (
    <div className="flex min-h-0 flex-col gap-3">
      <SchemaCard
        title="Input schema"
        description={tool.hasInput ? tool.inputType : 'No input required'}
        schema={tool.inputSchema}
      />

      <SchemaCard
        title="Output schema"
        description={tool.outputType}
        schema={tool.outputSchema}
        className="min-h-0 flex-1"
      />
    </div>
  );
}

function SchemaCard({
  title,
  description,
  schema,
  className = '',
}: {
  title: string;
  description: string;
  schema: ToolJsonSchema;
  className?: string;
}) {
  const [copied, setCopied] = useState(false);
  const [showRawJson, setShowRawJson] = useState(false);

  const json = useMemo(() => JSON.stringify(schema, null, 2), [schema]);
  const properties = getSchemaProperties(schema);
  const hasProperties = properties.length > 0;

  async function copySchema() {
    try {
      await navigator.clipboard.writeText(json);
      setCopied(true);

      window.setTimeout(() => {
        setCopied(false);
      }, 1200);
    } catch {
      setCopied(false);
    }
  }

  return (
    <InspectorCard className={className}>
      <div className="mb-3 flex items-start justify-between gap-3">
        <div className="min-w-0">
          <div className="text-xs font-semibold uppercase tracking-[0.14em] text-zinc-500 dark:text-zinc-500">
            {title}
          </div>

          <div className="mt-1 truncate font-mono text-xs text-zinc-500 dark:text-zinc-500">
            {description}
          </div>
        </div>

        <button
          type="button"
          onClick={copySchema}
          className="inline-flex size-7 shrink-0 items-center justify-center rounded-md border border-zinc-200 bg-white text-zinc-500 transition hover:text-zinc-950 dark:border-zinc-800 dark:bg-zinc-950 dark:text-zinc-500 dark:hover:text-zinc-100"
          title={copied ? 'Copied' : 'Copy schema'}
        >
          {copied ? <Check className="size-3.5" /> : <Copy className="size-3.5" />}
        </button>
      </div>

      {hasProperties ? (
        <div className="space-y-2">
          {properties.map(([propertyName, propertySchema]) => (
            <SchemaPropertyRow
              key={propertyName}
              name={propertyName}
              schema={propertySchema}
              required={isRequired(schema, propertyName)}
            />
          ))}
        </div>
      ) : (
        <div className="rounded-md border border-zinc-200 bg-white px-3 py-2 text-sm text-zinc-500 dark:border-zinc-800 dark:bg-zinc-950/70 dark:text-zinc-500">
          No fields.
        </div>
      )}

      <button
        type="button"
        onClick={() => setShowRawJson((current) => !current)}
        className="mt-3 inline-flex items-center gap-2 text-xs font-medium text-zinc-500 transition hover:text-zinc-950 dark:text-zinc-500 dark:hover:text-zinc-100"
      >
        <FileJson className="size-3.5" />
        {showRawJson ? 'Hide raw JSON' : 'Show raw JSON'}
      </button>

      {showRawJson && (
        <div className="mt-3">
          <SchemaPreview value={json} />
        </div>
      )}
    </InspectorCard>
  );
}

function SchemaPropertyRow({
  name,
  schema,
  required,
}: {
  name: string;
  schema: ToolJsonSchema;
  required: boolean;
}) {
  return (
    <div className="rounded-md border border-zinc-200 bg-white px-3 py-2 dark:border-zinc-800 dark:bg-zinc-950/70">
      <div className="flex items-center justify-between gap-3">
        <div className="min-w-0">
          <div className="truncate text-sm font-medium text-zinc-950 dark:text-zinc-100">
            {schema.title || formatDisplayName(name)}
          </div>

          <div className="mt-0.5 truncate font-mono text-xs text-zinc-500 dark:text-zinc-500">
            {name}
          </div>
        </div>

        <div className="flex shrink-0 items-center gap-2">
          <span className="rounded-full border border-zinc-200 bg-zinc-50 px-2 py-0.5 font-mono text-xs text-zinc-700 dark:border-zinc-800 dark:bg-zinc-900 dark:text-zinc-300">
            {formatSchemaType(schema)}
          </span>

          {required && (
            <span className="rounded-full border border-red-200 bg-red-50 px-2 py-0.5 text-xs font-medium text-red-700 dark:border-red-900/60 dark:bg-red-950/20 dark:text-red-300">
              Required
            </span>
          )}
        </div>
      </div>
    </div>
  );
}

function InspectorCard({
  children,
  className = '',
}: {
  children: ReactNode;
  className?: string;
}) {
  return (
    <section
      className={[
        'rounded-md border border-zinc-200 bg-zinc-50 p-3 dark:border-zinc-800 dark:bg-zinc-900/40',
        className,
      ].join(' ')}
    >
      {children}
    </section>
  );
}

function SchemaPreview({ value }: { value: string }) {
  return (
    <pre className="max-h-44 overflow-auto whitespace-pre-wrap break-words rounded-md border border-zinc-200 bg-white p-3 text-xs leading-6 text-zinc-800 [scrollbar-width:thin] [scrollbar-color:rgb(161_161_170)_transparent] dark:border-zinc-800 dark:bg-zinc-950/70 dark:text-zinc-300 dark:[scrollbar-color:rgb(82_82_91)_transparent] [&::-webkit-scrollbar]:w-1.5 [&::-webkit-scrollbar-track]:bg-transparent [&::-webkit-scrollbar-thumb]:rounded-full [&::-webkit-scrollbar-thumb]:bg-zinc-300 dark:[&::-webkit-scrollbar-thumb]:bg-zinc-700">
      {value}
    </pre>
  );
}

function getSchemaProperties(
  schema: ToolJsonSchema | undefined,
): [string, ToolJsonSchema][] {
  if (!schema?.properties) {
    return [];
  }

  return Object.entries(schema.properties);
}

function isRequired(schema: ToolJsonSchema | undefined, propertyName: string): boolean {
  return Boolean(schema?.required?.includes(propertyName));
}

function formatSchemaType(schema: ToolJsonSchema): string {
  if (schema.type === 'array') {
    const itemType = schema.items?.type ?? 'object';

    return `${itemType}[]`;
  }

  if (schema.enum && schema.enum.length > 0) {
    return 'enum';
  }

  return schema.type ?? 'object';
}

function formatDisplayName(value: string): string {
  return value
    .replace(/[-_]+/g, ' ')
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    .trim()
    .split(/\s+/)
    .filter(Boolean)
    .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
    .join(' ');
}
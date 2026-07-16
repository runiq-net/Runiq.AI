CREATE TABLE IF NOT EXISTS __SCHEMA__.schema_migrations (
    version integer PRIMARY KEY,
    applied_at timestamptz NOT NULL DEFAULT now()
);
CREATE TABLE IF NOT EXISTS __SCHEMA__.rag_indexes (
    index_name text PRIMARY KEY,
    embedding_model text NOT NULL DEFAULT '',
    embedding_dimension integer NOT NULL CHECK (embedding_dimension > 0),
    metric text NOT NULL CHECK (metric IN ('cosine', 'dot_product', 'euclidean')),
    metadata jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
);
CREATE TABLE IF NOT EXISTS __SCHEMA__.rag_documents (
    index_name text NOT NULL REFERENCES __SCHEMA__.rag_indexes(index_name) ON DELETE CASCADE,
    document_id text NOT NULL,
    source text NOT NULL DEFAULT '',
    title text NOT NULL DEFAULT '',
    content_hash text NOT NULL,
    version text NOT NULL DEFAULT '',
    metadata jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (index_name, document_id)
);
CREATE TABLE IF NOT EXISTS __SCHEMA__.rag_chunks (
    index_name text NOT NULL,
    document_id text NOT NULL,
    chunk_id text NOT NULL,
    content text NOT NULL,
    chunk_order integer NOT NULL DEFAULT 0,
    token_count integer NULL,
    metadata jsonb NOT NULL DEFAULT '{}'::jsonb,
    embedding vector NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (index_name, chunk_id),
    UNIQUE (index_name, document_id, chunk_id),
    FOREIGN KEY (index_name, document_id) REFERENCES __SCHEMA__.rag_documents(index_name, document_id) ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS __SCHEMA__.rag_ingestion_states (
    index_name text NOT NULL,
    document_id text NOT NULL,
    content_hash text NOT NULL,
    version text NOT NULL DEFAULT '',
    status text NOT NULL,
    ingested_at timestamptz NULL,
    failure_reason text NULL,
    PRIMARY KEY (index_name, document_id),
    FOREIGN KEY (index_name, document_id) REFERENCES __SCHEMA__.rag_documents(index_name, document_id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS ix_rag_documents_hash ON __SCHEMA__.rag_documents(index_name, content_hash);
CREATE INDEX IF NOT EXISTS ix_rag_chunks_document ON __SCHEMA__.rag_chunks(index_name, document_id, chunk_order);
CREATE INDEX IF NOT EXISTS ix_rag_chunks_metadata ON __SCHEMA__.rag_chunks USING gin(metadata jsonb_path_ops);
INSERT INTO __SCHEMA__.schema_migrations(version) VALUES (1) ON CONFLICT DO NOTHING;

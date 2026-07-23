CREATE EXTENSION IF NOT EXISTS pg_trgm;
ALTER TABLE __SCHEMA__.rag_chunks
    ADD COLUMN IF NOT EXISTS lexical_search tsvector
    GENERATED ALWAYS AS (to_tsvector('simple', coalesce(content, ''))) STORED;
CREATE INDEX IF NOT EXISTS ix_rag_chunks_lexical_search
    ON __SCHEMA__.rag_chunks USING gin(lexical_search);
CREATE INDEX IF NOT EXISTS ix_rag_chunks_identifier_search
    ON __SCHEMA__.rag_chunks USING gin(lower(content) gin_trgm_ops);
INSERT INTO __SCHEMA__.schema_migrations(version) VALUES (2) ON CONFLICT DO NOTHING;

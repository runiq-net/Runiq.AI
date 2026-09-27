# Runiq.AI.Memory.PostgreSql

Explicit PostgreSQL persistence for Memory conversations. See [Memory persistence](../../docs/memory-persistence.md) for registration, migrations, retry semantics and validation.

Migration `002_turns.sql` adds versioned turn lifecycle state while preserving existing messages and retry receipts. Apply migrations before starting upgraded hosts. See [multi-turn runtime behavior](../../docs/memory-conversations.md) for restart, concurrency, and unfinished-turn semantics.

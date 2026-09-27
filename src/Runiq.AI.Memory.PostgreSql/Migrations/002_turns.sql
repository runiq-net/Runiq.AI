-- Message JSON v1 and existing receipt payloads remain unchanged.
CREATE TABLE __SCHEMA__.turns (
    boundary_id text COLLATE "C" NOT NULL,
    thread_id text COLLATE "C" NOT NULL,
    turn_id text COLLATE "C" NOT NULL,
    payload text NOT NULL,
    payload_version integer NOT NULL CHECK (payload_version > 0),
    PRIMARY KEY (boundary_id, thread_id, turn_id),
    FOREIGN KEY (boundary_id, thread_id) REFERENCES __SCHEMA__.conversations (boundary_id, thread_id)
);
-- A terminal-only append has an empty range [version + 1, version].
ALTER TABLE __SCHEMA__.append_receipts DROP CONSTRAINT append_receipts_check;
ALTER TABLE __SCHEMA__.append_receipts ADD CONSTRAINT append_receipts_check CHECK (version >= first_sequence - 1);

CREATE TABLE __SCHEMA__.conversations (
    boundary_id text COLLATE "C" NOT NULL,
    thread_id text COLLATE "C" NOT NULL,
    thread_order bytea NOT NULL,
    resource_id text COLLATE "C" NOT NULL,
    agent_id text COLLATE "C" NOT NULL,
    sharing_group text COLLATE "C",
    created_at timestamptz NOT NULL,
    version bigint NOT NULL DEFAULT 0 CHECK (version >= 0),
    PRIMARY KEY (boundary_id, thread_id)
);
-- Keep the key below the B-tree row limit even for uncompressible maximum-length Unicode identifiers:
-- two UTF-8 identifiers (at most 768 bytes each) plus the UTF-16 ordinal key (at most 512 bytes).
-- Agent and sharing predicates remain exact SQL filters on the tenant/resource candidate rows.
CREATE INDEX conversations_scope ON __SCHEMA__.conversations (boundary_id, resource_id, thread_order);

CREATE FUNCTION __SCHEMA__.preserve_ownership() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF ROW(NEW.boundary_id, NEW.thread_id, NEW.thread_order, NEW.resource_id, NEW.agent_id, NEW.sharing_group, NEW.created_at)
       IS DISTINCT FROM ROW(OLD.boundary_id, OLD.thread_id, OLD.thread_order, OLD.resource_id, OLD.agent_id, OLD.sharing_group, OLD.created_at) THEN
        RAISE EXCEPTION 'Memory conversation ownership is immutable' USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER immutable_ownership BEFORE UPDATE ON __SCHEMA__.conversations
    FOR EACH ROW EXECUTE FUNCTION __SCHEMA__.preserve_ownership();

CREATE TABLE __SCHEMA__.messages (
    boundary_id text COLLATE "C" NOT NULL,
    thread_id text COLLATE "C" NOT NULL,
    message_id text COLLATE "C" NOT NULL,
    run_id text COLLATE "C" NOT NULL,
    sequence bigint NOT NULL CHECK (sequence > 0),
    payload_version integer NOT NULL CHECK (payload_version > 0),
    payload text NOT NULL,
    PRIMARY KEY (boundary_id, thread_id, message_id),
    UNIQUE (boundary_id, thread_id, sequence),
    FOREIGN KEY (boundary_id, thread_id) REFERENCES __SCHEMA__.conversations (boundary_id, thread_id)
);

CREATE TABLE __SCHEMA__.append_receipts (
    boundary_id text COLLATE "C" NOT NULL,
    thread_id text COLLATE "C" NOT NULL,
    request_key text COLLATE "C" NOT NULL,
    request_payload text NOT NULL,
    first_sequence bigint NOT NULL CHECK (first_sequence > 0),
    version bigint NOT NULL CHECK (version >= first_sequence),
    PRIMARY KEY (boundary_id, thread_id, request_key),
    FOREIGN KEY (boundary_id, thread_id) REFERENCES __SCHEMA__.conversations (boundary_id, thread_id)
);

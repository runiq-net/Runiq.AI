using Npgsql;
using System.Text;
using Runiq.AI.Memory.Abstractions;
using Runiq.AI.Memory.Configuration;
using Runiq.AI.Memory.Models;
using Runiq.AI.Memory.Serialization;
using Runiq.AI.Memory.Services;
using Runiq.AI.Memory.Validation;

namespace Runiq.AI.Memory.PostgreSql.Persistence;

internal sealed class PostgreSqlConversationStore(MemoryDatabase database, IMemoryAccessPolicy policy)
    : IMemoryConversationStore, IMemoryOwnershipLookup
{
    private const string ConversationColumns = "boundary_id, thread_id, resource_id, agent_id, sharing_group, created_at, version";
    private const string ScopeFilter = """
        boundary_id=@boundary AND resource_id=@resource AND sharing_group IS NOT DISTINCT FROM @sharing::text
        AND (@sharing::text IS NOT NULL OR agent_id=@agent)
        """;

    public ValueTask<MemoryThreadOwnership?> FindAsync(string boundaryId, string threadId, CancellationToken cancellationToken) =>
        MemoryDatabase.ExecuteAsync<MemoryThreadOwnership?>(async () =>
        {
            MemoryIdentifier.Validate(boundaryId, nameof(boundaryId));
            MemoryIdentifier.Validate(threadId, nameof(threadId));
            await using var connection = await database.DataSource.OpenConnectionAsync(cancellationToken);
            await using var command = database.Command(connection, null,
                $"SELECT {ConversationColumns} FROM __SCHEMA__.conversations WHERE boundary_id=@boundary AND thread_id=@thread",
                ("boundary", boundaryId), ("thread", threadId));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken) ? Conversation(reader).Ownership : null;
        });

    public ValueTask<MemoryConversation> CreateAsync(MemoryContext context, CancellationToken cancellationToken = default) =>
        MemoryDatabase.ExecuteAsync(async () =>
        {
            ArgumentNullException.ThrowIfNull(context);
            await MemoryStoreValidation.DemandAsync(policy, context, context.Ownership, cancellationToken);
            await using var connection = await database.DataSource.OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            if (context.IsNewThread)
            {
                var owner = context.Ownership.Scope;
                await using var create = database.Command(connection, transaction, """
                    INSERT INTO __SCHEMA__.conversations (boundary_id,thread_id,thread_order,resource_id,agent_id,sharing_group,created_at)
                    VALUES (@boundary,@thread,@order,@resource,@agent,@sharing,clock_timestamp())
                    ON CONFLICT (boundary_id,thread_id) DO NOTHING
                    """, ("boundary", owner.BoundaryId), ("thread", context.Ownership.ThreadId),
                    ("order", Encoding.BigEndianUnicode.GetBytes(context.Ownership.ThreadId)),
                    ("resource", owner.ResourceId), ("agent", owner.AgentId), ("sharing", owner.SharingGroup));
                await create.ExecuteNonQueryAsync(cancellationToken);
            }
            await using var query = database.Command(connection, transaction,
                $"SELECT {ConversationColumns} FROM __SCHEMA__.conversations WHERE boundary_id=@boundary AND thread_id=@thread FOR UPDATE",
                ("boundary", context.Identity.BoundaryId), ("thread", context.Ownership.ThreadId));
            MemoryConversation conversation;
            await using (var reader = await query.ExecuteReaderAsync(cancellationToken))
            {
                if (!await reader.ReadAsync(cancellationToken)) throw new MemoryStoreException(MemoryStoreError.AccessDenied);
                conversation = Conversation(reader);
            }
            if (conversation.Ownership != context.Ownership) throw new MemoryStoreException(MemoryStoreError.OwnershipConflict);
            await transaction.CommitAsync(cancellationToken);
            return conversation;
        });

    public ValueTask<MemoryConversation> ReadAsync(MemoryContext context, CancellationToken cancellationToken = default) =>
        MemoryDatabase.ExecuteAsync(async () =>
        {
            ArgumentNullException.ThrowIfNull(context);
            await using var connection = await database.DataSource.OpenConnectionAsync(cancellationToken);
            return await AuthorizeAsync(connection, null, context, false, cancellationToken);
        });

    public ValueTask<IReadOnlyList<MemoryConversation>> ListAsync(MemoryContext context, string? afterThreadId = null,
        int limit = 100, CancellationToken cancellationToken = default) =>
        MemoryDatabase.ExecuteAsync<IReadOnlyList<MemoryConversation>>(async () =>
        {
            ArgumentNullException.ThrowIfNull(context);
            MemoryStoreValidation.Page(limit, afterThreadId: afterThreadId);
            await using var connection = await database.DataSource.OpenConnectionAsync(cancellationToken);
            await AuthorizeAsync(connection, null, context, false, cancellationToken);
            var result = new List<MemoryConversation>();
            var cursor = afterThreadId;
            while (result.Count < limit)
            {
                await using var command = ScopedCommand(connection, null, context,
                    $"SELECT {ConversationColumns} FROM __SCHEMA__.conversations WHERE {ScopeFilter} " +
                    "AND (@resourceScope OR thread_id=@thread) AND (@cursor::bytea IS NULL OR thread_order>@cursor) ORDER BY thread_order LIMIT @limit");
                command.Parameters.AddWithValue("resourceScope", context.Scope == MemoryScope.Resource);
                // UTF-16 big-endian bytes match .NET ordinal ordering even for supplementary Unicode characters.
                command.Parameters.AddWithValue("cursor", cursor is null ? DBNull.Value : Encoding.BigEndianUnicode.GetBytes(cursor));
                command.Parameters.AddWithValue("limit", limit - result.Count);
                var candidates = new List<MemoryConversation>();
                await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
                    while (await reader.ReadAsync(cancellationToken)) candidates.Add(Conversation(reader));
                if (candidates.Count == 0) break;
                foreach (var candidate in candidates)
                    if (await MemoryAuthorizationService.CanAccessOwnershipAsync(policy, context.Identity, context.AccessScope, candidate.Ownership, cancellationToken))
                        result.Add(candidate);
                cursor = candidates[^1].Ownership.ThreadId;
            }
            return result.AsReadOnly();
        });

    public ValueTask<IReadOnlyList<StoredMemoryMessage>> ReadMessagesAsync(MemoryContext context, long afterSequence = 0,
        int limit = 100, CancellationToken cancellationToken = default) =>
        MemoryDatabase.ExecuteAsync<IReadOnlyList<StoredMemoryMessage>>(async () =>
        {
            ArgumentNullException.ThrowIfNull(context);
            MemoryStoreValidation.Page(limit, afterSequence);
            await using var connection = await database.DataSource.OpenConnectionAsync(cancellationToken);
            await AuthorizeAsync(connection, null, context, false, cancellationToken);
            return await ReadMessagesAsync(connection, null, context, afterSequence, limit, cancellationToken);
        });

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<MemoryTurn>> ReadTurnsAsync(MemoryContext context, CancellationToken cancellationToken = default) =>
        MemoryDatabase.ExecuteAsync<IReadOnlyList<MemoryTurn>>(async () =>
        {
            ArgumentNullException.ThrowIfNull(context);
            await using var connection = await database.DataSource.OpenConnectionAsync(cancellationToken);
            await AuthorizeAsync(connection, null, context, false, cancellationToken);
            return await ReadTurnsAsync(connection, null, context, cancellationToken);
        });

    private async Task<IReadOnlyList<MemoryTurn>> ReadTurnsAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction,
        MemoryContext context, CancellationToken cancellationToken)
    {
        await using var command = database.Command(connection, transaction,
            "SELECT payload,payload_version,turn_id FROM __SCHEMA__.turns WHERE boundary_id=@boundary AND thread_id=@thread",
            ("boundary", context.Identity.BoundaryId), ("thread", context.Ownership.ThreadId));
        var turns = new List<MemoryTurn>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var turn = MemoryTurnSerializer.Deserialize(reader.GetString(0), reader.GetInt32(1));
            if (turn.TurnId != reader.GetString(2)) throw new MemoryStoreException(MemoryStoreError.InvalidPayload);
            turns.Add(turn);
        }
        return turns.AsReadOnly();
    }

    public ValueTask<MemoryAppendResult> AppendAsync(MemoryContext context, MemoryAppendRequest request,
        CancellationToken cancellationToken = default) => MemoryDatabase.ExecuteAsync(async () =>
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(request);
            await using var connection = await database.DataSource.OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            // PostgreSQL owns serialization across independent hosts; no process-local lock participates.
            var conversation = await AuthorizeAsync(connection, transaction, context, true, cancellationToken);
            var payload = MemoryMessageSerializer.RequestPayload(request);
            await using (var receipt = database.Command(connection, transaction, """
                SELECT request_payload,first_sequence,version FROM __SCHEMA__.append_receipts
                WHERE boundary_id=@boundary AND thread_id=@thread AND request_key=@key
                """, ("boundary", context.Identity.BoundaryId), ("thread", context.Ownership.ThreadId), ("key", request.IdempotencyKey)))
            await using (var reader = await receipt.ExecuteReaderAsync(cancellationToken))
            {
                if (await reader.ReadAsync(cancellationToken))
                {
                    if (reader.GetString(0) != payload) throw new MemoryStoreException(MemoryStoreError.IdempotencyConflict);
                    return new MemoryAppendResult(reader.GetInt64(1), reader.GetInt64(2));
                }
            }
            if (conversation.Version != request.ExpectedVersion) throw new MemoryStoreException(MemoryStoreError.VersionConflict);
            var existing = await ReadMessagesAsync(connection, transaction, context, 0, null, cancellationToken);
            var turns = await ReadTurnsAsync(connection, transaction, context, cancellationToken);
            TurnValidation.ValidateAppend(turns, existing.Select(m => m.Content), request);
            MessageValidation.ValidateAppend(existing.Select(m => m.Content), request);
            var result = new MemoryAppendResult(checked(request.ExpectedVersion + 1), checked(request.ExpectedVersion + request.Messages.Count));
            for (var index = 0; index < request.Messages.Count; index++)
            {
                var message = request.Messages[index];
                await using var insert = database.Command(connection, transaction, """
                    INSERT INTO __SCHEMA__.messages (boundary_id,thread_id,message_id,run_id,sequence,payload_version,payload)
                    VALUES (@boundary,@thread,@id,@run,@sequence,@format,@payload)
                    """, ("boundary", context.Identity.BoundaryId), ("thread", context.Ownership.ThreadId),
                    ("id", message.MessageId), ("run", message.RunId), ("sequence", result.FirstSequence + index),
                    ("format", MemoryMessageSerializer.CurrentVersion), ("payload", MemoryMessageSerializer.Serialize(message)));
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }
            if (request.Turn is { } turn)
            {
                await using var writeTurn = database.Command(connection, transaction,
                    "INSERT INTO __SCHEMA__.turns (boundary_id,thread_id,turn_id,payload,payload_version) VALUES (@boundary,@thread,@turn,@payload,@format) " +
                    "ON CONFLICT (boundary_id,thread_id,turn_id) DO UPDATE SET payload=EXCLUDED.payload,payload_version=EXCLUDED.payload_version",
                    ("boundary", context.Identity.BoundaryId), ("thread", context.Ownership.ThreadId),
                    ("turn", turn.TurnId), ("payload", MemoryTurnSerializer.Serialize(turn)), ("format", MemoryTurnSerializer.CurrentVersion));
                await writeTurn.ExecuteNonQueryAsync(cancellationToken);
            }
            await using (var finish = database.Command(connection, transaction, """
                UPDATE __SCHEMA__.conversations SET version=@version WHERE boundary_id=@boundary AND thread_id=@thread;
                INSERT INTO __SCHEMA__.append_receipts (boundary_id,thread_id,request_key,request_payload,first_sequence,version)
                VALUES (@boundary,@thread,@key,@payload,@first,@version);
                """, ("boundary", context.Identity.BoundaryId), ("thread", context.Ownership.ThreadId),
                ("key", request.IdempotencyKey), ("payload", payload), ("first", result.FirstSequence), ("version", result.Version)))
                await finish.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        });

    private async ValueTask<MemoryConversation> AuthorizeAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction,
        MemoryContext context, bool forUpdate, CancellationToken cancellationToken)
    {
        await using var query = ScopedCommand(connection, transaction, context,
            $"SELECT {ConversationColumns} FROM __SCHEMA__.conversations WHERE {ScopeFilter} AND thread_id=@thread" + (forUpdate ? " FOR UPDATE" : ""));
        MemoryConversation? conversation = null;
        await using (var reader = await query.ExecuteReaderAsync(cancellationToken))
            if (await reader.ReadAsync(cancellationToken)) conversation = Conversation(reader);
        await MemoryStoreValidation.DemandAsync(policy, context, conversation?.Ownership, cancellationToken);
        return conversation!;
    }

    private async Task<IReadOnlyList<StoredMemoryMessage>> ReadMessagesAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction,
        MemoryContext context, long after, int? limit, CancellationToken cancellationToken)
    {
        await using var query = database.Command(connection, transaction, """
            SELECT sequence,payload_version,payload,message_id,run_id FROM __SCHEMA__.messages
            WHERE boundary_id=@boundary AND thread_id=@thread AND sequence>@after ORDER BY sequence
            """ + (limit is null ? "" : " LIMIT @limit"),
            ("boundary", context.Identity.BoundaryId), ("thread", context.Ownership.ThreadId), ("after", after));
        if (limit is not null) query.Parameters.AddWithValue("limit", limit.Value);
        var messages = new List<StoredMemoryMessage>();
        await using var reader = await query.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var content = MemoryMessageSerializer.Deserialize(reader.GetString(2), reader.GetInt32(1));
            if (content.MessageId != reader.GetString(3) || content.RunId != reader.GetString(4))
                throw new MemoryStoreException(MemoryStoreError.InvalidPayload);
            messages.Add(new(context.Ownership.ThreadId, reader.GetInt64(0), reader.GetInt32(1), content));
        }
        return messages.AsReadOnly();
    }

    private NpgsqlCommand ScopedCommand(NpgsqlConnection connection, NpgsqlTransaction? transaction, MemoryContext context, string sql) =>
        database.Command(connection, transaction, sql, ("boundary", context.Identity.BoundaryId), ("thread", context.Ownership.ThreadId),
            ("resource", context.AccessScope.ResourceId), ("agent", context.AccessScope.AgentId), ("sharing", context.AccessScope.SharingGroup));

    private static MemoryConversation Conversation(NpgsqlDataReader reader) =>
        new(new(reader.GetString(1), new(reader.GetString(0), reader.GetString(2), reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4))), reader.GetFieldValue<DateTimeOffset>(5), reader.GetInt64(6));
}

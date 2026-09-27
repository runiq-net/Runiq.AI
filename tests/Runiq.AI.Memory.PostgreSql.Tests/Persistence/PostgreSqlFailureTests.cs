using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Runiq.AI.Core.AI.Chat;
using Runiq.AI.Memory.Abstractions;
using Runiq.AI.Memory.Configuration;
using Runiq.AI.Memory.Models;
using Runiq.AI.Memory.PostgreSql.DependencyInjection;
using Runiq.AI.Memory.PostgreSql.Tests.Infrastructure;
using Runiq.AI.Memory.Services;

namespace Runiq.AI.Memory.PostgreSql.Tests.Persistence;

public sealed class PostgreSqlFailureTests
{
    [Fact]
    // A server failure after the first insert rolls back the entire batch, version and receipt, allowing a safe retry.
    public async Task MidBatchDatabaseFailure_RollsBackEverything()
    {
        await using var fixture = new PostgreSqlTestDatabase();
        await fixture.Migrator.MigrateAsync();
        await using var host = Host(fixture);
        using var scope = host.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IMemoryConversationStore>();
        var context = await Context(scope.ServiceProvider);
        await store.CreateAsync(context);
        await fixture.ExecuteAsync("ALTER TABLE __SCHEMA__.messages ADD CONSTRAINT fail_second CHECK (sequence <> 2)");
        var request = new MemoryAppendRequest("key", 0, [Message("one"), Message("two")]);
        Assert.Equal(MemoryStoreError.StorageFailure,
            (await Assert.ThrowsAsync<MemoryStoreException>(() => store.AppendAsync(context, request).AsTask())).Error);
        Assert.Empty(await store.ReadMessagesAsync(context));
        Assert.Equal(0, (await store.ReadAsync(context)).Version);
        Assert.Equal(0L, await fixture.ExecuteAsync("SELECT count(*) FROM __SCHEMA__.append_receipts"));
        await fixture.ExecuteAsync("ALTER TABLE __SCHEMA__.messages DROP CONSTRAINT fail_second");
        Assert.Equal(new(1, 2), await store.AppendAsync(context, request));
    }

    [Fact]
    // Cancelling a real PostgreSQL row-lock wait leaves the transaction empty and the pool usable for a subsequent write.
    public async Task CancellationDuringDatabaseWait_RollsBackAndReusesPool()
    {
        await using var fixture = new PostgreSqlTestDatabase();
        await fixture.Migrator.MigrateAsync();
        await using var host = Host(fixture);
        using var scope = host.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IMemoryConversationStore>();
        var context = await Context(scope.ServiceProvider);
        await store.CreateAsync(context);
        await using var connection = await fixture.Database.DataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var block = fixture.Database.Command(connection, transaction, "SELECT * FROM __SCHEMA__.conversations FOR UPDATE"))
            await block.ExecuteNonQueryAsync();
        using var cancellation = new CancellationTokenSource();
        var request = new MemoryAppendRequest("key", 0, [Message("one")]);
        var append = store.AppendAsync(context, request, cancellation.Token).AsTask();
        // Observe the server lock wait rather than assuming a timing delay means the command reached PostgreSQL.
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!(bool)(await fixture.ExecuteAsync($"SELECT EXISTS(SELECT 1 FROM pg_stat_activity WHERE wait_event_type='Lock' AND query LIKE '%{fixture.Schema}%')"))!)
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("Append did not reach the database lock wait.");
            await Task.Delay(20);
        }
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => append.WaitAsync(TimeSpan.FromSeconds(10)));
        await transaction.RollbackAsync();
        Assert.Empty(await store.ReadMessagesAsync(context));
        Assert.Equal(new(1, 1), await store.AppendAsync(context, request));
    }

    [Theory]
    [InlineData("UPDATE __SCHEMA__.messages SET payload_version=99")]
    [InlineData("UPDATE __SCHEMA__.messages SET payload='{}'")]
    [InlineData("UPDATE __SCHEMA__.messages SET run_id='wrong'")]
    // Corrupt or unsupported persisted content produces a controlled error instead of incomplete history.
    public async Task InvalidPersistedPayload_FailsExplicitly(string corrupt)
    {
        await using var fixture = new PostgreSqlTestDatabase();
        await fixture.Migrator.MigrateAsync();
        await using var host = Host(fixture);
        using var scope = host.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IMemoryConversationStore>();
        var context = await Context(scope.ServiceProvider);
        await store.CreateAsync(context);
        await store.AppendAsync(context, new("key", 0, [Message("one")]));
        await fixture.ExecuteAsync(corrupt);
        Assert.Equal(MemoryStoreError.InvalidPayload,
            (await Assert.ThrowsAsync<MemoryStoreException>(() => store.ReadMessagesAsync(context).AsTask())).Error);
    }

    [Fact]
    // SQL constraints independently prevent ownership rebinding, duplicate identities/sequences, and cross-tenant message attachment.
    public async Task DatabaseConstraints_EnforceStructuralInvariants()
    {
        await using var fixture = new PostgreSqlTestDatabase();
        await fixture.Migrator.MigrateAsync();
        await using var host = Host(fixture);
        using var scope = host.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IMemoryConversationStore>();
        var context = await Context(scope.ServiceProvider);
        await store.CreateAsync(context);
        await store.AppendAsync(context, new("key", 0, [Message("one")]));
        foreach (var sql in new[] {
            "UPDATE __SCHEMA__.conversations SET resource_id='other'",
            "INSERT INTO __SCHEMA__.messages SELECT * FROM __SCHEMA__.messages",
            "INSERT INTO __SCHEMA__.messages SELECT boundary_id,thread_id,'other',run_id,sequence,payload_version,payload FROM __SCHEMA__.messages",
            "INSERT INTO __SCHEMA__.messages SELECT 'other',thread_id,message_id,run_id,sequence,payload_version,payload FROM __SCHEMA__.messages",
            "INSERT INTO __SCHEMA__.append_receipts SELECT * FROM __SCHEMA__.append_receipts" })
            await Assert.ThrowsAsync<PostgresException>(() => fixture.ExecuteAsync(sql));
        Assert.Equal(1, (await store.ReadAsync(context)).Version);
    }

    internal static ServiceProvider Host(PostgreSqlTestDatabase fixture) => new ServiceCollection()
        .AddScoped<IMemoryAccessPolicy, Policy>().AddRuniqMemoryPostgreSql(o =>
        { o.ConnectionString = PostgreSqlTestDatabase.ConnectionString; o.Schema = fixture.Schema; }).BuildServiceProvider();
    internal static async Task<MemoryContext> Context(IServiceProvider services) =>
        (await services.GetRequiredService<MemoryAuthorizationService>().AuthorizeAsync(new("caller", "tenant"),
            new("resource"), "agent", new MemoryOptions()))!;
    private static MemoryMessage Message(string id) => new(id, "run", DateTimeOffset.UnixEpoch, new(ChatRole.User, id));
    private sealed class Policy : IMemoryAccessPolicy
    {
        public ValueTask<bool> CanAccessResourceAsync(MemoryIdentity identity, string resourceId, CancellationToken cancellationToken) => ValueTask.FromResult(true);
    }
}

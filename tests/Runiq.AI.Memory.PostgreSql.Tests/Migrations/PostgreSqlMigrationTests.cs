using Microsoft.Extensions.DependencyInjection;
using Runiq.AI.Core.AI.Chat;
using Runiq.AI.Memory.Abstractions;
using Runiq.AI.Memory.Configuration;
using Runiq.AI.Memory.Models;
using Runiq.AI.Memory.PostgreSql.DependencyInjection;
using Runiq.AI.Memory.PostgreSql.Migrations;
using Runiq.AI.Memory.PostgreSql.Persistence;
using Runiq.AI.Memory.PostgreSql.Tests.Infrastructure;
using Runiq.AI.Memory.Services;
using Runiq.AI.Memory.Serialization;

namespace Runiq.AI.Memory.PostgreSql.Tests.Migrations;

public sealed class PostgreSqlMigrationTests
{
    [Fact]
    // Independent pools serialize fresh migrations in PostgreSQL and repeated invocation preserves one history entry.
    public async Task FreshRepeatedConcurrentInitialization_IsSafe()
    {
        await using var fixture = new PostgreSqlTestDatabase();
        await using var other = new MemoryDatabase(fixture.Options);
        await Task.WhenAll(fixture.Migrator.MigrateAsync(), new PostgreSqlMemoryMigrator(other).MigrateAsync());
        await fixture.Migrator.MigrateAsync();
        Assert.Equal(1L, await fixture.ExecuteAsync("SELECT count(*) FROM __SCHEMA__.migration_history"));
        Assert.Equal(0L, await fixture.ExecuteAsync("SELECT count(*) FROM __SCHEMA__.messages"));
    }

    [Fact]
    // A failed initial migration leaves neither partial tables nor a falsely successful history row.
    public async Task FailedMigration_RollsBackSchemaAndHistory()
    {
        await using var fixture = new PostgreSqlTestDatabase();
        var error = await Assert.ThrowsAsync<MemoryStoreException>(() => fixture.Migrator.ApplyAsync([
            "CREATE TABLE __SCHEMA__.partial(id integer); SELECT missing_function();"]));
        Assert.Equal(MemoryStoreError.StorageFailure, error.Error);
        Assert.Equal(false, await fixture.ExecuteAsync($"SELECT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname='{fixture.Schema}')"));
        await fixture.Migrator.MigrateAsync();
        Assert.Equal(1L, await fixture.ExecuteAsync("SELECT count(*) FROM __SCHEMA__.migration_history"));
    }

    [Fact]
    // A fresh store reads real tool-linked history and replays its original receipt after a fixture upgrade; downgrade still fails.
    public async Task UpgradeFixture_PreservesDataAndRejectsDowngrade()
    {
        await using var fixture = new PostgreSqlTestDatabase();
        var initial = await PostgreSqlTestDatabase.InitialSqlAsync();
        await fixture.Migrator.MigrateAsync();
        ServiceProvider CreateHost() => new ServiceCollection()
            .AddScoped<IMemoryAccessPolicy, MigrationAccessPolicy>()
            .AddRuniqMemoryPostgreSql(options =>
            {
                options.ConnectionString = PostgreSqlTestDatabase.ConnectionString;
                options.Schema = fixture.Schema;
            }).BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        var identity = new MemoryIdentity("caller", "tenant");
        var options = new MemoryOptions(MemoryScope.Resource, "team");
        var timestamp = new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.FromHours(3)).AddTicks(1234567);
        var request = new MemoryAppendRequest("original-request", 0, [
            new("user", "run", timestamp, new(ChatRole.User, "question")),
            new("assistant", "run", timestamp.AddTicks(1), new(ChatRole.Assistant, "", ToolCalls: [new("call", "tool", "{\"x\":1}")])),
            new("result", "run", timestamp.AddTicks(2), new(ChatRole.Tool, "answer", "call"))]);
        MemoryConversation before;
        MemoryAppendResult originalReceipt;
        await using (var host = CreateHost())
        {
            await using var scope = host.CreateAsyncScope();
            var authorization = scope.ServiceProvider.GetRequiredService<MemoryAuthorizationService>();
            var context = await authorization.AuthorizeAsync(identity, new("resource"), "agent", options);
            Assert.NotNull(context);
            var store = scope.ServiceProvider.GetRequiredService<IMemoryConversationStore>();
            await store.CreateAsync(context);
            originalReceipt = await store.AppendAsync(context, request);
            before = await store.ReadAsync(context);
        }
        await Assert.ThrowsAsync<MemoryStoreException>(() => fixture.Migrator.ApplyAsync([initial,
            "CREATE TABLE __SCHEMA__.partial(id integer); SELECT missing_function();"]));
        Assert.Equal(1L, await fixture.ExecuteAsync("SELECT count(*) FROM __SCHEMA__.migration_history"));
        await fixture.Migrator.ApplyAsync([initial, "CREATE TABLE __SCHEMA__.upgrade_fixture(id integer);"]);
        // A new container owns a new pool and store; reads cannot be satisfied by pre-upgrade process state.
        await using (var host = CreateHost())
        {
            await using var scope = host.CreateAsyncScope();
            var authorization = scope.ServiceProvider.GetRequiredService<MemoryAuthorizationService>();
            var context = await authorization.AuthorizeAsync(identity, new("resource", before.Ownership.ThreadId), "agent", options);
            Assert.NotNull(context);
            Assert.False(context.IsNewThread);
            Assert.Equal(before.Ownership, context.Ownership);
            Assert.Equal(new MemoryAccessScope("tenant", "resource", "agent", "team"), context.AccessScope);
            Assert.Equal(MemoryScope.Resource, context.Scope);
            var store = scope.ServiceProvider.GetRequiredService<IMemoryConversationStore>();
            Assert.Equal(before, await store.ReadAsync(context));
            Assert.Equal(3, before.Version);
            var messages = await store.ReadMessagesAsync(context);
            Assert.Equal(3, messages.Count);
            for (var index = 0; index < request.Messages.Count; index++)
            {
                var expected = request.Messages[index];
                var actual = messages[index];
                Assert.Equal(before.Ownership.ThreadId, actual.ThreadId);
                Assert.Equal(index + 1, actual.Sequence);
                Assert.Equal(1, actual.PayloadVersion);
                Assert.Equal(expected.MessageId, actual.Content.MessageId);
                Assert.Equal(expected.RunId, actual.Content.RunId);
                Assert.Equal(expected.Timestamp.Ticks, actual.Content.Timestamp.Ticks);
                Assert.Equal(expected.Timestamp.Offset, actual.Content.Timestamp.Offset);
                Assert.Equal(expected.Message.Role, actual.Content.Message.Role);
                Assert.Equal(expected.Message.Content, actual.Content.Message.Content);
                Assert.Equal(expected.Message.ToolCallId, actual.Content.Message.ToolCallId);
                Assert.Equal<ChatToolCall>(expected.Message.ToolCalls ?? [], actual.Content.Message.ToolCalls ?? []);
            }
            Assert.Equal(Assert.Single(messages[1].Content.Message.ToolCalls!).Id, messages[2].Content.Message.ToolCallId);
            Assert.Equal(originalReceipt, await store.AppendAsync(context, request));
            var afterRetry = await store.ReadMessagesAsync(context);
            Assert.Equal(messages.Select(message => message.Sequence), afterRetry.Select(message => message.Sequence));
            Assert.Equal(messages.Select(message => MemoryMessageSerializer.Serialize(message.Content)),
                afterRetry.Select(message => MemoryMessageSerializer.Serialize(message.Content)));
            Assert.Equal(before, await store.ReadAsync(context));
            Assert.Equal(3L, await fixture.ExecuteAsync("SELECT count(*) FROM __SCHEMA__.messages"));
            Assert.Equal(1L, await fixture.ExecuteAsync("SELECT count(*) FROM __SCHEMA__.append_receipts"));
        }
        var error = await Assert.ThrowsAsync<MemoryStoreException>(() => fixture.Migrator.MigrateAsync());
        Assert.Equal(MemoryStoreError.IncompatibleSchema, error.Error);
        Assert.Equal(2L, await fixture.ExecuteAsync("SELECT count(*) FROM __SCHEMA__.migration_history"));
    }

    [Fact]
    // History checksums reject altered prior migrations rather than silently accepting incompatible tables.
    public async Task ChangedHistory_FailsExplicitly()
    {
        await using var fixture = new PostgreSqlTestDatabase();
        await fixture.Migrator.MigrateAsync();
        await fixture.ExecuteAsync("UPDATE __SCHEMA__.migration_history SET checksum='changed'");
        Assert.Equal(MemoryStoreError.IncompatibleSchema,
            (await Assert.ThrowsAsync<MemoryStoreException>(() => fixture.Migrator.MigrateAsync())).Error);
    }

    private sealed class MigrationAccessPolicy : IMemoryAccessPolicy
    {
        public ValueTask<bool> CanAccessResourceAsync(MemoryIdentity identity, string resourceId, CancellationToken cancellationToken) =>
            ValueTask.FromResult(identity == new MemoryIdentity("caller", "tenant") && resourceId == "resource");

        public ValueTask<bool> CanShareAsync(MemoryIdentity identity, MemoryAccessScope owner, MemoryAccessScope requested,
            CancellationToken cancellationToken) => ValueTask.FromResult(owner == requested && owner.SharingGroup == "team");
    }
}

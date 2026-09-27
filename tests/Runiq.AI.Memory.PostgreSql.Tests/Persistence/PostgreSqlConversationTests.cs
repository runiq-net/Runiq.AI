using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Runiq.AI.Core.AI.Chat;
using Runiq.AI.Memory.Abstractions;
using Runiq.AI.Memory.Models;
using Runiq.AI.Memory.PostgreSql.DependencyInjection;
using Runiq.AI.Memory.PostgreSql.Tests.Infrastructure;
using Runiq.AI.Memory.Tests.Persistence;

namespace Runiq.AI.Memory.PostgreSql.Tests.Persistence;

public sealed class PostgreSqlConversationTests : ConversationStoreScenarios
{
    private readonly PostgreSqlTestDatabase fixture = new();

    [Fact]
    // A literal pre-continuation receipt remains retryable, while new writes exclude runtime state and still reject changed durable content.
    public async Task Continuation_PreservesLegacyIdempotencyAndDurablePayload()
    {
        const string legacy = """{"MessageId":"m","RunId":"r","Timestamp":"1970-01-01T00:00:00+00:00","Message":{"Role":2,"Content":"answer","ToolCallId":null,"ToolCalls":null}}""";
        const string receipt = "{\"ExpectedVersion\":0,\"Messages\":[" + legacy + "]}";
        using var scope = Host.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IMemoryConversationStore>();
        var context = Proposal();
        await store.CreateAsync(context);
        await using (var connection = await fixture.Database.DataSource.OpenConnectionAsync())
        await using (var command = fixture.Database.Command(connection, null,
            "INSERT INTO __SCHEMA__.messages VALUES (@boundary,@thread,'m','r',1,1,@payload); " +
            "INSERT INTO __SCHEMA__.append_receipts VALUES (@boundary,@thread,'key',@receipt,1,1); " +
            "UPDATE __SCHEMA__.conversations SET version=1 WHERE boundary_id=@boundary AND thread_id=@thread;",
            ("boundary", context.Identity.BoundaryId), ("thread", context.Ownership.ThreadId),
            ("payload", legacy), ("receipt", receipt)))
            await command.ExecuteNonQueryAsync();

        var runtime = new ChatMessage(ChatRole.Assistant, "answer")
        {
            Continuation = new("openai.responses.output",
                JsonSerializer.SerializeToElement(new[] { new { type = "reasoning", encrypted_content = "secret" } }), 100)
        };
        var request = new MemoryAppendRequest("key", 0, [new("m", "r", DateTimeOffset.UnixEpoch, runtime)]);
        Assert.Equal(new MemoryAppendResult(1, 1), await store.AppendAsync(context, request));
        Assert.Equal(new MemoryAppendResult(1, 1), await store.AppendAsync(context,
            new("key", 0, [new("m", "r", DateTimeOffset.UnixEpoch, runtime with { Continuation = null })])));
        Assert.Equal(legacy, await fixture.ExecuteAsync("SELECT payload FROM __SCHEMA__.messages"));
        Assert.Equal(receipt, await fixture.ExecuteAsync("SELECT request_payload FROM __SCHEMA__.append_receipts"));
        Assert.Null(Assert.Single(await store.ReadMessagesAsync(context)).Content.Message.Continuation);

        var fresh = Proposal(thread: "fresh");
        await store.CreateAsync(fresh);
        var result = await store.AppendAsync(fresh, request);
        Assert.Equal(result, await store.AppendAsync(fresh, request));
        Assert.Null(Assert.Single(await store.ReadMessagesAsync(fresh)).Content.Message.Continuation);
        Assert.Equal(2L, await fixture.ExecuteAsync("SELECT count(*) FROM __SCHEMA__.messages"));
        Assert.Equal(1L, await fixture.ExecuteAsync("SELECT count(DISTINCT payload) FROM __SCHEMA__.messages"));
        Assert.Equal(1L, await fixture.ExecuteAsync("SELECT count(DISTINCT request_payload) FROM __SCHEMA__.append_receipts"));
        Assert.NotNull(runtime.Continuation);
        var conflict = await Assert.ThrowsAsync<MemoryStoreException>(() => store.AppendAsync(context,
            new("key", 0, [new("m", "r", DateTimeOffset.UnixEpoch, runtime with { Content = "changed" })])).AsTask());
        Assert.Equal(MemoryStoreError.IdempotencyConflict, conflict.Error);
    }

    protected override ServiceProvider CreateHost() => new ServiceCollection()
        .AddSingleton<IMemoryAccessPolicy>(Policy).AddRuniqMemoryPostgreSql(o =>
        { o.ConnectionString = PostgreSqlTestDatabase.ConnectionString; o.Schema = fixture.Schema; }).BuildServiceProvider();

    public override async Task InitializeAsync()
    {
        await fixture.Migrator.MigrateAsync();
        await base.InitializeAsync();
    }

    public override async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await fixture.DisposeAsync();
    }
}

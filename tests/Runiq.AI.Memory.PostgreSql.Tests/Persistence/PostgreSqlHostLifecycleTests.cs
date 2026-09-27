using System.Text.Json;
using Runiq.AI.Memory.PostgreSql.Tests.Infrastructure;

namespace Runiq.AI.Memory.PostgreSql.Tests.Persistence;

public sealed class PostgreSqlHostLifecycleTests
{
    [Theory]
    [InlineData("model-second")]
    [InlineData("model-stream")]
    // A fresh process invokes the real runtime and receives prior user, assistant, and tool messages without tool re-execution.
    public async Task RuntimeProcessRestart_ReplaysConversation(string mode)
    {
        await using var fixture = new PostgreSqlTestDatabase();
        await fixture.Migrator.MigrateAsync();
        using var writer = new MemoryHostProcess("model-first", fixture.Schema);
        using var first = JsonDocument.Parse(await writer.FinishAsync());
        var thread = first.RootElement.GetProperty("ThreadId").GetString()!;
        Assert.Equal(1, first.RootElement.GetProperty("ToolCalls").GetInt32());
        using var reader = new MemoryHostProcess(mode, fixture.Schema, thread);
        using var second = JsonDocument.Parse(await reader.FinishAsync());
        Assert.Equal(thread, second.RootElement.GetProperty("ThreadId").GetString());
        Assert.Equal(0, second.RootElement.GetProperty("ToolCalls").GetInt32());
        var messages = second.RootElement.GetProperty("Messages").EnumerateArray().ToArray();
        Assert.Equal(6, messages.Length);
        Assert.Equal("My name is Ada", messages[1].GetProperty("Content").GetString());
        Assert.Equal("call", messages[2].GetProperty("ToolCalls")[0].GetProperty("Id").GetString());
        Assert.Equal("call", messages[3].GetProperty("ToolCallId").GetString());
        Assert.Equal("Ada", messages[4].GetProperty("Content").GetString());
        Assert.Equal(2L, await fixture.ExecuteAsync("SELECT count(*) FROM __SCHEMA__.turns WHERE (payload::json->>'Status')::integer=1"));
    }

    [Fact]
    // Durable turn reservation has one winner across independent processes even when the winner exits without finalizing.
    public async Task IndependentProcesses_ReserveOneTurn()
    {
        await using var fixture = new PostgreSqlTestDatabase();
        await fixture.Migrator.MigrateAsync();
        using var writer = new MemoryHostProcess("write", fixture.Schema);
        using var original = JsonDocument.Parse(await writer.FinishAsync());
        var thread = original.RootElement.GetProperty("Conversation").GetProperty("Ownership").GetProperty("ThreadId").GetString()!;
        using var first = new MemoryHostProcess("turn-race", fixture.Schema, thread, "one");
        using var second = new MemoryHostProcess("turn-race", fixture.Schema, thread, "two");
        await Task.WhenAll(first.ReadyAsync(), second.ReadyAsync());
        await Task.WhenAll(first.ReleaseAsync(), second.ReleaseAsync());
        var outcomes = (await Task.WhenAll(first.FinishAsync(), second.FinishAsync()))
            .Select(s => JsonSerializer.Deserialize<JsonElement>(s)).ToArray();
        Assert.Single(outcomes, o => o.GetProperty("Success").GetBoolean());
        Assert.Single(outcomes, o => !o.GetProperty("Success").GetBoolean());
        Assert.Equal(1L, await fixture.ExecuteAsync("SELECT count(*) FROM __SCHEMA__.turns WHERE (payload::json->>'Status')::integer=0"));
        Assert.Equal(4L, await fixture.ExecuteAsync("SELECT count(*) FROM __SCHEMA__.messages"));
    }

    [Fact]
    // Host A exits completely; a newly started host B recovers identical ownership, timestamps, tools and retry receipt.
    public async Task ProcessRestart_PreservesHistoryAndOriginalRetry()
    {
        await using var fixture = new PostgreSqlTestDatabase();
        await fixture.Migrator.MigrateAsync();
        using var writer = new MemoryHostProcess("write", fixture.Schema);
        var original = await writer.FinishAsync();
        using var json = JsonDocument.Parse(original);
        var thread = json.RootElement.GetProperty("Conversation").GetProperty("Ownership").GetProperty("ThreadId").GetString()!;
        await fixture.Migrator.MigrateAsync();
        using var reader = new MemoryHostProcess("retry", fixture.Schema, thread);
        Assert.Equal(original, await reader.FinishAsync());
        Assert.Equal(3L, await fixture.ExecuteAsync("SELECT count(*) FROM __SCHEMA__.messages"));
        Assert.Equal(1L, await fixture.ExecuteAsync("SELECT count(*) FROM __SCHEMA__.append_receipts"));
    }

    [Fact]
    // Two independently running hosts cross a start barrier and compete for one expected version in PostgreSQL.
    public async Task IndependentProcesses_HaveOneVersionWinner()
    {
        await using var fixture = new PostgreSqlTestDatabase();
        await fixture.Migrator.MigrateAsync();
        using var writer = new MemoryHostProcess("write", fixture.Schema);
        using var original = JsonDocument.Parse(await writer.FinishAsync());
        var thread = original.RootElement.GetProperty("Conversation").GetProperty("Ownership").GetProperty("ThreadId").GetString()!;
        using var first = new MemoryHostProcess("race", fixture.Schema, thread, "race-one");
        using var second = new MemoryHostProcess("race", fixture.Schema, thread, "race-two");
        await Task.WhenAll(first.ReadyAsync(), second.ReadyAsync());
        await Task.WhenAll(first.ReleaseAsync(), second.ReleaseAsync());
        var outputs = await Task.WhenAll(first.FinishAsync(), second.FinishAsync());
        var outcomes = outputs.Select(s => JsonSerializer.Deserialize<JsonElement>(s)).ToArray();
        Assert.Single(outcomes, o => o.GetProperty("Success").GetBoolean());
        Assert.Equal("VersionConflict", Assert.Single(outcomes, o => !o.GetProperty("Success").GetBoolean()).GetProperty("Error").GetString());
        Assert.Equal(4L, await fixture.ExecuteAsync("SELECT version FROM __SCHEMA__.conversations"));
        Assert.Equal(4L, await fixture.ExecuteAsync("SELECT count(*) FROM __SCHEMA__.messages"));
    }
}

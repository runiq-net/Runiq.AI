using System.Text.Json;
using Runiq.AI.Memory.PostgreSql.Tests.Infrastructure;

namespace Runiq.AI.Memory.PostgreSql.Tests.Persistence;

public sealed class PostgreSqlHostLifecycleTests
{
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

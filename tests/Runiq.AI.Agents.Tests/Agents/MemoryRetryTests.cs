using Microsoft.Extensions.DependencyInjection;
using Runiq.AI.Agents.Runtime;
using Runiq.AI.Memory.Abstractions;
using Runiq.AI.Memory.Models;
using static Runiq.AI.Agents.Tests.Agents.MemoryContinuationTests;
using static Runiq.AI.Agents.Tests.Agents.MemoryOutcomeTests;

namespace Runiq.AI.Agents.Tests.Agents;

public sealed class MemoryRetryTests
{
    [Fact]
    // Independent runtime scopes reject overlapping turns before model work and retain the winning transcript.
    public async Task OverlappingTurns_ConflictBeforeModelExecution()
    {
        var client = new Client();
        using var host = Services(client).BuildServiceProvider();
        using var firstScope = host.CreateScope();
        using var secondScope = host.CreateScope();
        var runtime = firstScope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>();
        await using var stream = runtime.ExecuteStreamAsync("agent", new AgentQuery("first")
            { Memory = new("resource"), MemoryTurnId = "first-turn" }).GetAsyncEnumerator();
        Assert.True(await stream.MoveNextAsync());
        var thread = stream.Current.ThreadId;
        Assert.True(await stream.MoveNextAsync());
        var other = await secondScope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>().ExecuteAsync("agent",
            new AgentQuery("overlap") { Memory = new("resource", thread), MemoryTurnId = "other-turn" });
        Assert.Equal("MemoryTurnConflict", other.ErrorCode);
        Assert.Single(client.Requests);
        Assert.True(await stream.MoveNextAsync());
        Assert.Equal(AgentExecutionEventKind.Completed, stream.Current.Kind);
        var turns = await firstScope.ServiceProvider.GetRequiredService<IMemoryConversationStore>()
            .ReadTurnsAsync(await Context(firstScope.ServiceProvider, thread!));
        Assert.Equal(MemoryTurnStatus.Completed, Assert.Single(turns).Status);
    }

    [Theory]
    [InlineData("question")]
    [InlineData("changed question")]
    // Retained logical identity rejects identical and changed duplicate invocations without replaying tools or user writes.
    public async Task DuplicateInvocation_IsRejected(string repeatedInput)
    {
        var client = new Client();
        using var host = Services(client).BuildServiceProvider();
        using var scope = host.CreateScope();
        var runtime = scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>();
        var first = await runtime.ExecuteAsync("agent", new AgentQuery("question") { Memory = new("resource"), MemoryTurnId = "request" });
        Assert.True(first.IsSuccess);
        var duplicate = await runtime.ExecuteAsync("agent", new AgentQuery(repeatedInput)
            { Memory = new("resource", first.ThreadId), MemoryTurnId = "request" });
        Assert.Equal("MemoryTurnConflict", duplicate.ErrorCode);
        Assert.Single(client.Requests);
        Assert.NotEqual(first.RunId, duplicate.RunId);
        var messages = await scope.ServiceProvider.GetRequiredService<IMemoryConversationStore>()
            .ReadMessagesAsync(await Context(scope.ServiceProvider, first.ThreadId!));
        Assert.Equal(2, messages.Count);
    }
}

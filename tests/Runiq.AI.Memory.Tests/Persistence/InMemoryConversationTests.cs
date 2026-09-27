using Microsoft.Extensions.DependencyInjection;
using Runiq.AI.Memory.Abstractions;
using Runiq.AI.Memory.DependencyInjection;

namespace Runiq.AI.Memory.Tests.Persistence;

public sealed class InMemoryConversationTests : ConversationStoreScenarios
{
    protected override ServiceProvider CreateHost() => new ServiceCollection()
        .AddSingleton<IMemoryAccessPolicy>(Policy).AddRuniqMemoryInMemory().BuildServiceProvider();

    [Fact]
    // Volatile storage survives request scopes but a newly constructed host has no previous data.
    public async Task IndependentHost_StartsEmpty()
    {
        using (var scope = Host.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IMemoryConversationStore>().CreateAsync(Proposal());
        using (var scope = Host.CreateScope())
            Assert.NotNull(await scope.ServiceProvider.GetRequiredService<IMemoryOwnershipLookup>().FindAsync("tenant", "thread", default));
        await using var fresh = CreateHost();
        using var next = fresh.CreateScope();
        Assert.Null(await next.ServiceProvider.GetRequiredService<IMemoryOwnershipLookup>().FindAsync("tenant", "thread", default));
    }
}

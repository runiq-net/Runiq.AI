using Microsoft.Extensions.DependencyInjection;
using Runiq.AI.Core;
using Runiq.AI.Memory.Abstractions;
using Runiq.AI.Memory.Configuration;

namespace Runiq.AI.Agents.Tests.Agents;

public sealed class AgentMemoryConfigurationTests
{
    [Fact]
    // Ordinary registration keeps Memory disabled and needs no host Memory adapters.
    public void Registration_DoesNotImplicitlyEnableMemory()
    {
        var agent = new Agent("agent", "Agent", "instructions", "openai/model");
        var services = new ServiceCollection();
        services.AddRuniqServer(options => options.AddAgent(agent));
        Assert.Null(agent.Memory);
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IMemoryOwnershipLookup));
    }

    [Fact]
    // Shared definitions retain only immutable settings and reject conflicting reconfiguration.
    public void OptIn_RetainsImmutableSettings()
    {
        var settings = new MemoryOptions(MemoryScope.Resource, "support");
        var agent = new Agent("agent", "Agent", "instructions", "openai/model").UseMemory(settings);
        Assert.Same(settings, agent.Memory);
        Assert.Throws<InvalidOperationException>(() => agent.UseMemory());
        new ServiceCollection().AddRuniqServer(options => options.AddAgent(agent));
    }
}

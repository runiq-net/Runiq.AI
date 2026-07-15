using Microsoft.Extensions.DependencyInjection;

using Runiq.AI.Agents;
using Runiq.AI.ContextSpaces.Models.Sources;

namespace Runiq.AI.Core.Tests.Configuration;

public sealed class RuniqServerServiceCollectionExtensionsTests
{
    [Fact]
    public void AddRuniqServer_ShouldRegisterContextSpacesAsReadOnlyList()
    {
        // AddRuniqServer ile tanimlanan ContextSpace kayitlarinin DI container'a tasindigini dogrular.
        var services = new ServiceCollection();

        services.AddRuniqServer(options =>
        {
            options.AddContextSpace(new ContextSpace(
                id: "travel-planning",
                name: "Travel Planning Context"));
        });

        using var provider = services.BuildServiceProvider();

        var contextSpaces = provider.GetRequiredService<IReadOnlyList<ContextSpace>>();
        var contextSpace = Assert.Single(contextSpaces);

        Assert.Equal("travel-planning", contextSpace.Id);
        Assert.Equal("Travel Planning Context", contextSpace.Name);
    }

    [Fact]
    public void AddRuniqServer_ShouldRegisterEmptyContextSpaceList_WhenNoContextSpaceIsConfigured()
    {
        // ContextSpace tanimlanmasa bile DI üzerinden bos liste çözümlenebildigini dogrular.
        var services = new ServiceCollection();

        services.AddRuniqServer(_ => { });

        using var provider = services.BuildServiceProvider();

        var contextSpaces = provider.GetRequiredService<IReadOnlyList<ContextSpace>>();

        Assert.Empty(contextSpaces);
    }

    [Fact]
    public void AddRuniqServer_ShouldAllowAgentToUseRegisteredContextSpace()
    {
        // Agent'in kayitli bir ContextSpace'e baglanabildigini dogrular.
        var services = new ServiceCollection();

        services.AddRuniqServer(options =>
        {
            options.AddContextSpace(new ContextSpace(
                id: "travel-planning",
                name: "Travel Planning Context"));

            options.AddAgent(new Agent(
                    id: "travel-agent",
                    name: "Travel Agent",
                    instructions: "Plan short travel routes.",
                    model: "openai/gpt-5")
                .UseContextSpace("travel-planning"));
        });

        using var provider = services.BuildServiceProvider();

        var agents = provider.GetServices<Agent>().ToArray();
        var agent = Assert.Single(agents);

        var contextSpaceId = Assert.Single(agent.ContextSpaceIds);
        Assert.Equal("travel-planning", contextSpaceId);
    }

    [Fact]
    public void AddRuniqServer_ShouldThrow_WhenAgentUsesUnknownContextSpace()
    {
        // Agent'in kayitli olmayan bir ContextSpace'e baglanmasi durumunda startup validasyonunun hata verdigini dogrular.
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddRuniqServer(options =>
            {
                options.AddAgent(new Agent(
                        id: "travel-agent",
                        name: "Travel Agent",
                        instructions: "Plan short travel routes.",
                        model: "openai/gpt-5")
                    .UseContextSpace("missing-context"));
            }));

        Assert.Contains("travel-agent", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("missing-context", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddRuniqServer_ShouldNotValidateDashboardAuthentication_WhenDashboardIsNotUsed()
    {
        // Sadece agent kaydi yapilan uygulamada Dashboard auth validation'in devreye girmedigini dogrular.
        var services = new ServiceCollection();

        services.AddRuniqServer(options =>
        {
            options.AddAgent(new Agent(
                id: "travel-agent",
                name: "Travel Agent",
                instructions: "Plan short travel routes.",
                model: "openai/gpt-5"));
        });

        using var provider = services.BuildServiceProvider();

        var agent = Assert.Single(provider.GetServices<Agent>());

        Assert.Equal("travel-agent", agent.Id);
    }


}


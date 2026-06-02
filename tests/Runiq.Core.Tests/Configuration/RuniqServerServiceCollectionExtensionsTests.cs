using Microsoft.Extensions.DependencyInjection;

using Runiq.Agents;
using Runiq.ContextSpaces.Models.Sources;

namespace Runiq.Core.Tests.Configuration;

public sealed class RuniqServerServiceCollectionExtensionsTests
{
    [Fact]
    public void AddRuniqServer_ShouldRegisterContextSpacesAsReadOnlyList()
    {
        // AddRuniqServer ile tanımlanan ContextSpace kayıtlarının DI container'a taşındığını doğrular.
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
        // ContextSpace tanımlanmasa bile DI üzerinden boş liste çözümlenebildiğini doğrular.
        var services = new ServiceCollection();

        services.AddRuniqServer(_ => { });

        using var provider = services.BuildServiceProvider();

        var contextSpaces = provider.GetRequiredService<IReadOnlyList<ContextSpace>>();

        Assert.Empty(contextSpaces);
    }

    [Fact]
    public void AddRuniqServer_ShouldAllowAgentToUseRegisteredContextSpace()
    {
        // Agent'ın kayıtlı bir ContextSpace'e bağlanabildiğini doğrular.
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
        // Agent'ın kayıtlı olmayan bir ContextSpace'e bağlanması durumunda startup validasyonunun hata verdiğini doğrular.
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
        // Sadece agent kaydı yapılan uygulamada Dashboard auth validation'ın devreye girmediğini doğrular.
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

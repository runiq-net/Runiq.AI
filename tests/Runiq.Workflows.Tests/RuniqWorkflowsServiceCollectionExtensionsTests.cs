using Runiq.Workflows.Services;
using Runiq.Workflows.Interfaces;
using Runiq.Workflows.Infrastructure;
using Runiq.Workflows.Domain;
using Runiq.Workflows.Models;
using Microsoft.Extensions.DependencyInjection;
using Runiq.Agents;
using Runiq.Workflows.Tests.Fakes;

namespace Runiq.Workflows.Tests;

public sealed class RuniqWorkflowsServiceCollectionExtensionsTests
{
    /// <summary>
    /// AddRuniqWorkflows Ã§aÄŸrÄ±sÄ±nÄ±n boÅŸ workflow registry kaydettiÄŸini doÄŸrular.
    /// </summary>
    [Fact]
    public void AddRuniqWorkflows_ShouldRegisterEmptyRegistry()
    {
        var services = new ServiceCollection();

        services.AddRuniqWorkflows();

        using var serviceProvider = services.BuildServiceProvider();

        var registry = serviceProvider.GetRequiredService<FlowCatalog>();

        Assert.Empty(registry.Flows);
    }

    /// <summary>
    /// Options Ã¼zerinden eklenen workflow tanÄ±mÄ±nÄ±n registry'ye taÅŸÄ±ndÄ±ÄŸÄ±nÄ± doÄŸrular.
    /// </summary>
    [Fact]
    public void AddRuniqWorkflows_ShouldRegisterConfiguredFlow()
    {
        var workflow = CreateFlow("travel");
        var services = new ServiceCollection();

        services.AddRuniqWorkflows(options =>
        {
            options.AddFlow(workflow);
        });

        using var serviceProvider = services.BuildServiceProvider();

        var registry = serviceProvider.GetRequiredService<FlowCatalog>();
        var registeredFlow = Assert.Single(registry.Flows);

        Assert.Same(workflow, registeredFlow);
        Assert.Same(workflow, registry.FindById("travel"));
    }

    /// <summary>
    /// AynÄ± workflow id ile ikinci kayÄ±t eklendiÄŸinde yapÄ±landÄ±rmanÄ±n hata verdiÄŸini doÄŸrular.
    /// </summary>
    [Fact]
    public void AddRuniqWorkflows_ShouldThrow_WhenFlowIdAlreadyExists()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            services.AddRuniqWorkflows(options =>
            {
                options.AddFlow(CreateFlow("travel"));
                options.AddFlow(CreateFlow("TRAVEL"));
            });
        });

        Assert.Contains("Flow with id 'TRAVEL' is already registered.", exception.Message);
    }

    /// <summary>
    /// GeÃ§ersiz workflow tanÄ±mÄ± options Ã¼zerinden kaydedildiÄŸinde doÄŸrulama hatasÄ± fÄ±rlatÄ±ldÄ±ÄŸÄ±nÄ± doÄŸrular.
    /// </summary>
    [Fact]
    public void AddRuniqWorkflows_ShouldThrow_WhenFlowIsInvalid()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            services.AddRuniqWorkflows(options =>
            {
                options.AddFlow(new Flow("empty", "Empty"));
            });
        });

        Assert.Contains("Flow 'empty' is invalid:", exception.Message);
        Assert.Contains("Flow must contain at least one step.", exception.Message);
    }

    /// <summary>
    /// AddRuniqWorkflows Ã§aÄŸrÄ±sÄ±nÄ±n workflow execution runtime sÃ¶zleÅŸmesini Ã§Ã¶zÃ¼lebilir hale getirdiÄŸini doÄŸrular.
    /// </summary>
    [Fact]
    public void AddRuniqWorkflows_ShouldResolveFlowRunner()
    {
        var services = new ServiceCollection();

        services.AddSingleton<Agent>(new TestAgent());
        services.AddRuniqWorkflows();
        services.AddSingleton<IAgentStepExecutor, FakeRuniqAgentStepExecutor>();

        using var serviceProvider = services.BuildServiceProvider();

        var runtime = serviceProvider.GetRequiredService<IFlowRunner>();

        Assert.IsType<FlowRunner>(runtime);
    }

    /// <summary>
    /// AddRuniqWorkflows Ã§aÄŸrÄ±sÄ±nÄ±n workflow registry servisini Ã§Ã¶zÃ¼lebilir hale getirdiÄŸini doÄŸrular.
    /// </summary>
    [Fact]
    public void AddRuniqWorkflows_ShouldResolveFlowCatalog()
    {
        var services = new ServiceCollection();

        services.AddRuniqWorkflows();

        using var serviceProvider = services.BuildServiceProvider();

        var registry = serviceProvider.GetRequiredService<FlowCatalog>();

        Assert.NotNull(registry);
    }

    private static Flow CreateFlow(string id)
    {
        return new Flow(id, "Test Flow")
            .Step<TestAgent>("begin")
                .OnFailureStop()
            .Build();
    }

    private sealed class TestAgent : Agent
    {
        public TestAgent()
            : base(
                id: "test-agent",
                name: "Test Agent",
                instructions: "Test instructions.",
                model: "openai/gpt-5")
        {
        }
    }
}

using Runiq.AI.Workflows.Services;
using Runiq.AI.Workflows.Interfaces;
using Runiq.AI.Workflows.Infrastructure;
using Runiq.AI.Workflows.Domain;
using Runiq.AI.Workflows.Models;
using Microsoft.Extensions.DependencyInjection;
using Runiq.AI.Agents;
using Runiq.AI.Workflows.Tests.Fakes;

namespace Runiq.AI.Workflows.Tests;

public sealed class RuniqWorkflowsServiceCollectionExtensionsTests
{
    /// <summary>
    /// AddRuniqWorkflows çağrısının boş workflow registry kaydettiğini doğrular.
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
    /// Options üzerinden eklenen workflow tanımının registry'ye taşındığını doğrular.
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
    /// Aynı workflow id ile ikinci kayıt eklendiğinde yapılandırmanın hata verdiğini doğrular.
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
    /// Geçersiz workflow tanımı options üzerinden kaydedildiğinde doğrulama hatası fırlatıldığını doğrular.
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
    /// AddRuniqWorkflows çağrısının workflow execution runtime sözleşmesini çözülebilir hale getirdiğini doğrular.
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
    /// AddRuniqWorkflows çağrısının workflow registry servisini çözülebilir hale getirdiğini doğrular.
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


using Microsoft.Extensions.DependencyInjection;
using Runiq.Agents;
using Runiq.Workflows.Tests.Fakes;

namespace Runiq.Workflows.Tests;

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

        var registry = serviceProvider.GetRequiredService<WorkflowRegistry>();

        Assert.Empty(registry.Workflows);
    }

    /// <summary>
    /// Options üzerinden eklenen workflow tanımının registry'ye taşındığını doğrular.
    /// </summary>
    [Fact]
    public void AddRuniqWorkflows_ShouldRegisterConfiguredWorkflow()
    {
        var workflow = CreateWorkflow("travel");
        var services = new ServiceCollection();

        services.AddRuniqWorkflows(options =>
        {
            options.AddWorkflow(workflow);
        });

        using var serviceProvider = services.BuildServiceProvider();

        var registry = serviceProvider.GetRequiredService<WorkflowRegistry>();
        var registeredWorkflow = Assert.Single(registry.Workflows);

        Assert.Same(workflow, registeredWorkflow);
        Assert.Same(workflow, registry.FindById("travel"));
    }

    /// <summary>
    /// Aynı workflow id ile ikinci kayıt eklendiğinde yapılandırmanın hata verdiğini doğrular.
    /// </summary>
    [Fact]
    public void AddRuniqWorkflows_ShouldThrow_WhenWorkflowIdAlreadyExists()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            services.AddRuniqWorkflows(options =>
            {
                options.AddWorkflow(CreateWorkflow("travel"));
                options.AddWorkflow(CreateWorkflow("TRAVEL"));
            });
        });

        Assert.Contains("Workflow with id 'TRAVEL' is already registered.", exception.Message);
    }

    /// <summary>
    /// Geçersiz workflow tanımı options üzerinden kaydedildiğinde doğrulama hatası fırlatıldığını doğrular.
    /// </summary>
    [Fact]
    public void AddRuniqWorkflows_ShouldThrow_WhenWorkflowIsInvalid()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            services.AddRuniqWorkflows(options =>
            {
                options.AddWorkflow(new Workflow("empty", "Empty"));
            });
        });

        Assert.Contains("Workflow 'empty' is invalid:", exception.Message);
        Assert.Contains("Workflow must contain at least one step.", exception.Message);
    }

    /// <summary>
    /// AddRuniqWorkflows çağrısının workflow execution runtime sözleşmesini çözülebilir hale getirdiğini doğrular.
    /// </summary>
    [Fact]
    public void AddRuniqWorkflows_ShouldResolveWorkflowExecutionRuntime()
    {
        var services = new ServiceCollection();

        services.AddSingleton<Agent>(new TestAgent());
        services.AddRuniqWorkflows();
        services.AddSingleton<IWorkflowAgentExecutor, FakeWorkflowAgentExecutor>();

        using var serviceProvider = services.BuildServiceProvider();

        var runtime = serviceProvider.GetRequiredService<IWorkflowExecutionRuntime>();

        Assert.IsType<WorkflowExecutionRuntime>(runtime);
    }

    /// <summary>
    /// AddRuniqWorkflows çağrısının workflow registry servisini çözülebilir hale getirdiğini doğrular.
    /// </summary>
    [Fact]
    public void AddRuniqWorkflows_ShouldResolveWorkflowRegistry()
    {
        var services = new ServiceCollection();

        services.AddRuniqWorkflows();

        using var serviceProvider = services.BuildServiceProvider();

        var registry = serviceProvider.GetRequiredService<WorkflowRegistry>();

        Assert.NotNull(registry);
    }

    private static Workflow CreateWorkflow(string id)
    {
        return new Workflow(id, "Test Workflow")
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

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Runiq.Agents;
using Runiq.Core;
using Runiq.Workflows;

namespace Runiq.Core.Tests.Dashboard;

/// <summary>
/// Dashboard workflow metadata endpoint davranışlarını doğrulayan testleri içerir.
/// </summary>
[Collection("Dashboard assets")]
public sealed class DashboardWorkflowEndpointTests
{
    [Fact]
    public async Task WorkflowsEndpoint_ShouldReturnRegisteredWorkflows()
    {
        // Workflow liste endpoint'inin registry'deki workflow tanımlarını döndürdüğünü doğrular.
        using var server = CreateServer();

        var response = await server.CreateClient().GetAsync("/dashboard/api/workflows");

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        var workflows = document.RootElement;

        Assert.Equal(JsonValueKind.Array, workflows.ValueKind);

        var workflow = Assert.Single(workflows.EnumerateArray());

        Assert.Equal("travel-planning-workflow", workflow.GetProperty("id").GetString());
        Assert.Equal("Travel Planning Workflow", workflow.GetProperty("name").GetString());

        var steps = workflow.GetProperty("steps");

        Assert.Equal(3, steps.GetArrayLength());
        Assert.Equal("weather", steps[0].GetProperty("id").GetString());
        Assert.Equal(nameof(WeatherAgent), steps[0].GetProperty("agentName").GetString());
        Assert.Equal("places", steps[0].GetProperty("successStepId").GetString());
        Assert.Equal("Continue", steps[0].GetProperty("failureBehavior").GetString());
        Assert.Equal("places", steps[0].GetProperty("failureStepId").GetString());
    }

    [Fact]
    public async Task WorkflowDetailEndpoint_ShouldReturnWorkflowById()
    {
        // Workflow detay endpoint'inin id ile istenen workflow metadata bilgisini döndürdüğünü doğrular.
        using var server = CreateServer();

        var response = await server
            .CreateClient()
            .GetAsync("/dashboard/api/workflows/travel-planning-workflow");

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        Assert.Equal(
            "travel-planning-workflow",
            document.RootElement.GetProperty("id").GetString());
        Assert.Equal("weather", document.RootElement.GetProperty("startStepId").GetString());
        Assert.Equal(3, document.RootElement.GetProperty("stepCount").GetInt32());
    }

    [Fact]
    public async Task WorkflowDetailEndpoint_ShouldReturnNotFound_WhenWorkflowIdIsUnknown()
    {
        // Bilinmeyen workflow id için detay endpoint'inin 404 döndürdüğünü doğrular.
        using var server = CreateServer();

        var response = await server
            .CreateClient()
            .GetAsync("/dashboard/api/workflows/missing-workflow");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task WorkflowRunEndpoint_ShouldReturnExecutionResult()
    {
        // Workflow run endpoint'inin runtime sonucunu dashboard DTO'suna map ettiğini doğrular.
        var runtime = new DashboardTestWorkflowRuntime();
        using var server = CreateServer(runtime);

        var response = await server
            .CreateClient()
            .PostAsJsonAsync(
                "/dashboard/api/workflows/travel-planning-workflow/run",
                new { input = "Istanbul trip" });

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var steps = root.GetProperty("steps");

        Assert.True(runtime.WasCalled);
        Assert.Equal("travel-planning-workflow", runtime.WorkflowId);
        Assert.Equal("Istanbul trip", runtime.Input);
        Assert.Equal("Completed", root.GetProperty("status").GetString());
        Assert.Equal("Planner output", root.GetProperty("finalOutput").GetString());
        Assert.Equal("planner", steps[0].GetProperty("stepId").GetString());
        Assert.Equal("PlannerAgent", steps[0].GetProperty("agentName").GetString());
        Assert.Equal("Istanbul trip", steps[0].GetProperty("input").GetString());
        Assert.Equal("Planner output", steps[0].GetProperty("output").GetString());

        var toolCalls = steps[0].GetProperty("toolCalls");
        var toolCall = Assert.Single(toolCalls.EnumerateArray());

        Assert.Equal("weather.search", toolCall.GetProperty("toolName").GetString());
        Assert.Equal("Completed", toolCall.GetProperty("status").GetString());
        Assert.Equal("""{"city":"Istanbul"}""", toolCall.GetProperty("argumentsJson").GetString());
        Assert.Equal("""{"condition":"clear"}""", toolCall.GetProperty("outputJson").GetString());
    }

    [Fact]
    public async Task WorkflowRunEndpoint_ShouldReturnNotFound_WhenWorkflowIdIsUnknown()
    {
        // Bilinmeyen workflow id için run endpoint'inin 404 döndürdüğünü doğrular.
        using var server = CreateServer(new DashboardTestWorkflowRuntime());

        var response = await server
            .CreateClient()
            .PostAsJsonAsync(
                "/dashboard/api/workflows/missing-workflow/run",
                new { input = "Istanbul trip" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task WorkflowRunEndpoint_ShouldReturnBadRequest_WhenInputIsEmpty()
    {
        // Boş workflow girdisinin runtime çalıştırılmadan reddedildiğini doğrular.
        var runtime = new DashboardTestWorkflowRuntime();
        using var server = CreateServer(runtime);

        var response = await server
            .CreateClient()
            .PostAsJsonAsync(
                "/dashboard/api/workflows/travel-planning-workflow/run",
                new { input = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(runtime.WasCalled);
    }

    [Fact]
    public async Task TeamApiEndpoint_ShouldReturnNotFound()
    {
        // Agent Team API endpoint'inin aktif dashboard API yüzeyinden kaldırıldığını doğrular.
        using var server = CreateServer();

        var response = await server
            .CreateClient()
            .PostAsync("/dashboard/api/teams/travel-team/chat", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public void WorkflowServices_ShouldBuildWithScopeValidationEnabled()
    {
        // Workflow runtime servislerinin scoped agent runtime ile uyumlu lifetime kullandığını doğrular.
        var services = new ServiceCollection();

        services.AddRuniqServer(options =>
        {
            options.AddAgent(new WeatherAgent());
            options.AddAgent(new PlacesAgent());
            options.AddAgent(new PlannerAgent());
        });
        services.AddRuniqWorkflows(options =>
        {
            options.AddWorkflow(CreateWorkflow());
        });

        using var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        using var scope = serviceProvider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IWorkflowExecutionRuntime>());
    }

    private static TestServer CreateServer(
        IWorkflowExecutionRuntime? workflowExecutionRuntime = null)
    {
        PrepareDashboardAssets();

        var builder = new WebHostBuilder()
            .UseContentRoot(AppContext.BaseDirectory)
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddRuniqServer();
                services.AddRuniqWorkflows(options =>
                {
                    options.AddWorkflow(CreateWorkflow());
                });

                if (workflowExecutionRuntime is not null)
                {
                    services.AddScoped(_ => workflowExecutionRuntime);
                }
            })
            .Configure(app =>
            {
                app.UseRuniqDashboard(options =>
                {
                    options.Path = "/dashboard";
                    options.Title = "Test Dashboard";
                });
            });

        return new TestServer(builder);
    }

    private static Workflow CreateWorkflow()
    {
        return new Workflow(
                id: "travel-planning-workflow",
                name: "Travel Planning Workflow")
            .Step<WeatherAgent>("weather")
                .OnSuccess("places")
                .OnFailureContinue("places")
            .Step<PlacesAgent>("places")
                .OnSuccess("planner")
                .OnFailureContinue("planner")
            .Step<PlannerAgent>("planner")
                .OnFailureStop()
            .Build();
    }

    private static void PrepareDashboardAssets()
    {
        var root = Path.Combine(
            AppContext.BaseDirectory,
            "Studio",
            "wwwroot");

        Directory.CreateDirectory(root);

        var indexPath = Path.Combine(root, "index.html");

        File.WriteAllText(
            indexPath,
            """
            <!doctype html>
            <html>
            <head>
                <title>__RUNIQ_TITLE_HTML__</title>
                <script>
                    window.__RUNIQ_DASHBOARD__ = __RUNIQ_DASHBOARD_CONFIG__;
                </script>
            </head>
            <body>Runiq Dashboard</body>
            </html>
            """);
    }

    private sealed class WeatherAgent : Agent
    {
        public WeatherAgent()
            : base("weather-agent", "Weather Agent", "Weather.", "openai/gpt-5")
        {
        }
    }

    private sealed class PlacesAgent : Agent
    {
        public PlacesAgent()
            : base("places-agent", "Places Agent", "Places.", "openai/gpt-5")
        {
        }
    }

    private sealed class PlannerAgent : Agent
    {
        public PlannerAgent()
            : base("planner-agent", "Planner Agent", "Plan.", "openai/gpt-5")
        {
        }
    }

    private sealed class DashboardTestWorkflowRuntime : IWorkflowExecutionRuntime
    {
        public bool WasCalled { get; private set; }

        public string? WorkflowId { get; private set; }

        public string? Input { get; private set; }

        public Task<WorkflowExecutionResult> ExecuteAsync(
            Workflow workflow,
            string input,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            WorkflowId = workflow.Id;
            Input = input;
            var startedAt = DateTimeOffset.Parse("2026-05-27T10:00:00+03:00");
            var completedAt = startedAt.AddMilliseconds(42);

            return Task.FromResult(new WorkflowExecutionResult(
                WorkflowExecutionStatus.Completed,
                [
                    new WorkflowStepExecutionResult(
                        stepId: "planner",
                        agentType: typeof(PlannerAgent),
                        status: WorkflowStepExecutionStatus.Completed,
                        input: input,
                        output: "Planner output",
                        toolCalls:
                        [
                            new WorkflowToolCallExecutionResult(
                                toolCallId: "call_weather",
                                toolName: "weather.search",
                                status: WorkflowToolCallExecutionStatus.Completed,
                                argumentsJson: """{"city":"Istanbul"}""",
                                outputJson: """{"condition":"clear"}""",
                                startedAt: startedAt,
                                completedAt: completedAt)
                        ])
                ],
                finalOutput: "Planner output"));
        }
    }
}

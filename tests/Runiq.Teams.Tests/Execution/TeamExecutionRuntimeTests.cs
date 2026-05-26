using Runiq.Agents;
using Runiq.Agents.Providers.OpenAI;
using Runiq.Agents.Runtime;
using Runiq.Agents.Tools;
using Runiq.ContextSpaces.Models.Sources;
using Runiq.ContextSpaces.Services;
using Runiq.Teams.Execution;
using Runiq.Teams.Execution.Planning;
using Runiq.Teams.Models.Execution;
using Runiq.Teams.Models.Teams;
using Microsoft.Extensions.DependencyInjection;

namespace Runiq.Teams.Tests.Execution;

/// <summary>
/// Agent team runtime yürütme davranışlarını doğrular.
/// </summary>
public sealed class TeamExecutionRuntimeTests
{
    /// <summary>
    /// Takım üyesi olarak tanımlanan agent runtime'da bulunamadığında member ve team fail eventlerinin üretildiğini doğrular.
    /// </summary>
    [Fact]
    public async Task ExecuteStreamAsync_ShouldFailTeam_WhenMemberAgentDoesNotExist()
    {
        var team = new AgentTeam(
                id: "travel-team",
                name: "Travel Planning Team",
                instructions: "Create travel plans.")
            .AddMember(
                agentId: "missing-agent",
                role: "Researcher");

        var runtime = CreateRuntime(
            teams: [team],
            agents: []);

        var events = await runtime.ExecuteStreamAsync(
                teamId: "travel-team",
                input: "Create a two day travel plan.")
            .ToListAsync();

        Assert.Collection(
            events,
            first =>
            {
                Assert.Equal(TeamExecutionEventType.TeamStarted, first.Type);
                Assert.Equal("travel-team", first.TeamId);
                Assert.Equal("Travel Planning Team", first.TeamName);
            },
            second =>
            {
                Assert.Equal(TeamExecutionEventType.MemberStarted, second.Type);
                Assert.Equal("missing-agent", second.MemberAgentId);
                Assert.Equal("Researcher", second.MemberRole);
            },
            third =>
            {
                Assert.Equal(TeamExecutionEventType.MemberFailed, third.Type);
                Assert.Equal("missing-agent", third.MemberAgentId);
                Assert.Equal("Researcher", third.MemberRole);
                Assert.Equal("AgentNotFound", third.ErrorCode);
                Assert.Equal("Agent 'missing-agent' was not found.", third.ErrorMessage);
            },
            fourth =>
            {
                Assert.Equal(TeamExecutionEventType.TeamFailed, fourth.Type);
                Assert.Equal("travel-team", fourth.TeamId);
                Assert.Equal("TeamMemberFailed", fourth.ErrorCode);
                Assert.Equal(
                    "Agent team member 'missing-agent' failed: Agent 'missing-agent' was not found.",
                    fourth.ErrorMessage);
            });
    }

    /// <summary>
    /// Takım üyesi stream yürütmesi beklenmeyen exception ürettiğinde member ve team hata eventlerinin ayrıntılı mesajla üretildiğini doğrular.
    /// </summary>
    [Fact]
    public async Task ExecuteStreamAsync_ShouldEmitMemberAndTeamFailure_WhenMemberExecutionThrows()
    {
        var contextSpace = new ContextSpace(
                id: "travel-context",
                name: "Travel Context")
            .AddSource(new ContextSpaceSource(
                id: "travel-source",
                name: "Travel Source",
                kind: ContextSpaceSourceKind.Database));

        var agent = new Agent(
                id: "planner-agent",
                name: "Planner Agent",
                instructions: "Create a plan.",
                model: "openai/gpt-5")
            .UseContextSpace("travel-context");

        var team = new AgentTeam(
                id: "travel-team",
                name: "Travel Planning Team",
                instructions: "Create travel plans.")
            .AddMember(
                agentId: "planner-agent",
                role: "Travel Planner");

        var runtime = CreateRuntime(
            teams: [team],
            agents: [agent],
            contextSpaces: [contextSpace],
            sourceSearchService: new ThrowingContextSpaceSourceSearchService(
                "Search index unavailable."));

        var events = await runtime.ExecuteStreamAsync(
                teamId: "travel-team",
                input: "Create a one day travel plan.")
            .ToListAsync();

        Assert.Collection(
            events,
            first =>
            {
                Assert.Equal(TeamExecutionEventType.TeamStarted, first.Type);
                Assert.Equal("travel-team", first.TeamId);
            },
            second =>
            {
                Assert.Equal(TeamExecutionEventType.MemberStarted, second.Type);
                Assert.Equal("planner-agent", second.MemberAgentId);
                Assert.Equal("Travel Planner", second.MemberRole);
            },
            third =>
            {
                Assert.Equal(TeamExecutionEventType.MemberFailed, third.Type);
                Assert.Equal("planner-agent", third.MemberAgentId);
                Assert.Equal("Travel Planner", third.MemberRole);
                Assert.Equal("MemberExecutionException", third.ErrorCode);
                Assert.Equal("Search index unavailable.", third.ErrorMessage);
            },
            fourth =>
            {
                Assert.Equal(TeamExecutionEventType.TeamFailed, fourth.Type);
                Assert.Equal("travel-team", fourth.TeamId);
                Assert.Equal("TeamMemberFailed", fourth.ErrorCode);
                Assert.Equal(
                    "Agent team member 'planner-agent' failed: Search index unavailable.",
                    fourth.ErrorMessage);
            });
    }

    /// <summary>
    /// Sıralı takım yürütmesinde yalnızca son üyenin delta event'lerinin final üye olarak işaretlendiğini doğrular.
    /// </summary>
    [Fact]
    public async Task ExecuteStreamAsync_ShouldMarkOnlyFinalMemberDeltasAsFinal()
    {
        var weatherAgent = new Agent(
            id: "weather-agent",
            name: "Weather Agent",
            instructions: "Provide weather findings.",
            model: "ollama/demo");

        var plannerAgent = new Agent(
            id: "planner-agent",
            name: "Planner Agent",
            instructions: "Create the final plan.",
            model: "ollama/demo");

        var team = new AgentTeam(
                id: "travel-team",
                name: "Travel Planning Team",
                instructions: "Create travel plans.")
            .AddMember(
                agentId: "weather-agent",
                role: "Weather Analyst")
            .AddMember(
                agentId: "planner-agent",
                role: "Travel Planner");

        var runtime = CreateRuntime(
            teams: [team],
            agents: [weatherAgent, plannerAgent]);

        var events = await runtime.ExecuteStreamAsync(
                teamId: "travel-team",
                input: "Create a one day travel plan.")
            .ToListAsync();

        var deltas = events
            .Where(executionEvent => executionEvent.Type == TeamExecutionEventType.MemberDelta)
            .ToArray();

        Assert.Contains(deltas, executionEvent =>
            executionEvent.MemberAgentId == "weather-agent" &&
            !executionEvent.IsFinalMember);

        Assert.Contains(deltas, executionEvent =>
            executionEvent.MemberAgentId == "planner-agent" &&
            executionEvent.IsFinalMember);
    }

    /// <summary>
    /// Runtime'ın ham takım üye sırası yerine planner tarafından dönen plan sırasını yürüttüğünü doğrular.
    /// </summary>
    [Fact]
    public async Task ExecuteStreamAsync_ShouldExecuteSelectedPlanOrder()
    {
        var weatherAgent = new Agent(
            id: "weather-agent",
            name: "Weather Agent",
            instructions: "Provide weather findings.",
            model: "ollama/demo");

        var plannerAgent = new Agent(
            id: "planner-agent",
            name: "Planner Agent",
            instructions: "Create the final plan.",
            model: "ollama/demo");

        var team = new AgentTeam(
                id: "travel-team",
                name: "Travel Planning Team",
                instructions: "Create travel plans.")
            .AddMember(
                agentId: "weather-agent",
                role: "Weather Analyst")
            .AddMember(
                agentId: "planner-agent",
                role: "Travel Planner");

        var plan = new TeamExecutionPlan(
            [
                new TeamExecutionPlanStep(
                    "planner-agent",
                    "Travel Planner",
                    "Selected first by test planner.",
                    0,
                    false),
                new TeamExecutionPlanStep(
                    "weather-agent",
                    "Weather Analyst",
                    "Selected final by test planner.",
                    1,
                    true)
            ],
            "weather-agent",
            "Test plan.");

        var runtime = CreateRuntime(
            teams: [team],
            agents: [weatherAgent, plannerAgent],
            plannerResolver: new FixedPlannerResolver(plan));

        var events = await runtime.ExecuteStreamAsync(
                teamId: "travel-team",
                input: "Create a one day travel plan.")
            .ToListAsync();

        var memberStartedEvents = events
            .Where(executionEvent => executionEvent.Type == TeamExecutionEventType.MemberStarted)
            .ToArray();

        Assert.Collection(
            memberStartedEvents,
            first => Assert.Equal("planner-agent", first.MemberAgentId),
            second => Assert.Equal("weather-agent", second.MemberAgentId));
    }

    /// <summary>
    /// Bilinmeyen team kimliği verildiğinde team not found event'i üretildiğini doğrular.
    /// </summary>
    [Fact]
    public async Task ExecuteStreamAsync_ShouldFail_WhenTeamDoesNotExist()
    {
        var runtime = CreateRuntime(
            teams: [],
            agents: []);

        var events = await runtime.ExecuteStreamAsync(
                teamId: "missing-team",
                input: "Create a plan.")
            .ToListAsync();

        var executionEvent = Assert.Single(events);

        Assert.Equal(TeamExecutionEventType.TeamFailed, executionEvent.Type);
        Assert.Equal("missing-team", executionEvent.TeamId);
        Assert.Equal("TeamNotFound", executionEvent.ErrorCode);
        Assert.Equal("Agent team 'missing-team' was not found.", executionEvent.ErrorMessage);
    }

    /// <summary>
    /// Boş kullanıcı girdisi verildiğinde input validation hatası üretildiğini doğrular.
    /// </summary>
    [Fact]
    public async Task ExecuteStreamAsync_ShouldFail_WhenInputIsEmpty()
    {
        var runtime = CreateRuntime(
            teams: [],
            agents: []);

        var events = await runtime.ExecuteStreamAsync(
                teamId: "travel-team",
                input: " ")
            .ToListAsync();

        var executionEvent = Assert.Single(events);

        Assert.Equal(TeamExecutionEventType.TeamFailed, executionEvent.Type);
        Assert.Equal("travel-team", executionEvent.TeamId);
        Assert.Equal("InputRequired", executionEvent.ErrorCode);
        Assert.Equal("Team input cannot be empty.", executionEvent.ErrorMessage);
    }

    /// <summary>
    /// Üyesi olmayan team çalıştırıldığında team has no members hatası üretildiğini doğrular.
    /// </summary>
    [Fact]
    public async Task ExecuteStreamAsync_ShouldFail_WhenTeamHasNoMembers()
    {
        var team = new AgentTeam(
            id: "empty-team",
            name: "Empty Team",
            instructions: "Does nothing.");

        var runtime = CreateRuntime(
            teams: [team],
            agents: []);

        var events = await runtime.ExecuteStreamAsync(
                teamId: "empty-team",
                input: "Create a plan.")
            .ToListAsync();

        var executionEvent = Assert.Single(events);

        Assert.Equal(TeamExecutionEventType.TeamFailed, executionEvent.Type);
        Assert.Equal("empty-team", executionEvent.TeamId);
        Assert.Equal("TeamHasNoMembers", executionEvent.ErrorCode);
        Assert.Equal("Agent team 'empty-team' does not have any members.", executionEvent.ErrorMessage);
    }

    private static TeamExecutionRuntime CreateRuntime(
        IReadOnlyList<AgentTeam> teams,
        IReadOnlyList<Agent> agents,
        IReadOnlyList<ContextSpace>? contextSpaces = null,
        IContextSpaceSourceSearchService? sourceSearchService = null,
        ITeamExecutionPlannerResolver? plannerResolver = null)
    {

        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var toolInvoker = new AgentToolInvoker(serviceProvider);

        var agentRuntime = new AgentExecutionRuntime(
            agents,
            CreateOpenAIResponsesClient(),
            CreateOpenAICompatibleClient(),
            toolInvoker,
            contextSpaces,
            sourceSearchService: sourceSearchService);

        return new TeamExecutionRuntime(
            teams,
            agentRuntime,
            toolInvoker,
            plannerResolver ?? CreatePlannerResolver());
    }

    private static ITeamExecutionPlannerResolver CreatePlannerResolver()
    {
        var sequentialPlanner = new SequentialTeamExecutionPlanner();

        return new TeamExecutionPlannerResolver(
            sequentialPlanner,
            new AdaptiveTeamExecutionPlanner(
                new ThrowingTeamPlanningModelClient(),
                sequentialPlanner));
    }

    private static OpenAIResponsesClient CreateOpenAIResponsesClient()
    {
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://example.test")
        };

        return new OpenAIResponsesClient(httpClient);
    }

    private static OpenAICompatibleClient CreateOpenAICompatibleClient()
    {
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://example.test")
        };

        return new OpenAICompatibleClient(httpClient);
    }

    private sealed class ThrowingContextSpaceSourceSearchService : IContextSpaceSourceSearchService
    {
        private readonly string message;

        public ThrowingContextSpaceSourceSearchService(string message)
        {
            this.message = message;
        }

        public Task<IReadOnlyList<ContextSpaceSourceSearchResult>> SearchAsync(
            ContextSpace contextSpace,
            string query,
            int maxResults = 5,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(message);
        }

        public Task<ContextSpaceSourceSearchResponse> SearchWithSummaryAsync(
            ContextSpace contextSpace,
            string query,
            int maxResults = 5,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class ThrowingTeamPlanningModelClient : ITeamPlanningModelClient
    {
        public Task<string> CreatePlanJsonAsync(
            AgentTeam team,
            string userInput,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Planning model should not be called by sequential tests.");
        }
    }

    private sealed class FixedPlannerResolver : ITeamExecutionPlannerResolver
    {
        private readonly TeamExecutionPlan plan;

        public FixedPlannerResolver(TeamExecutionPlan plan)
        {
            this.plan = plan;
        }

        public ITeamExecutionPlanner Resolve(AgentTeam team)
        {
            return new FixedPlanner(plan);
        }
    }

    private sealed class FixedPlanner : ITeamExecutionPlanner
    {
        private readonly TeamExecutionPlan plan;

        public FixedPlanner(TeamExecutionPlan plan)
        {
            this.plan = plan;
        }

        public Task<TeamExecutionPlan> CreatePlanAsync(
            AgentTeam team,
            string userInput,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(plan);
        }
    }
}

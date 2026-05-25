using Microsoft.Extensions.DependencyInjection;
using Runiq.Agents.Providers.OpenAI;
using Runiq.Agents.Runtime;
using Runiq.Agents.Tools;
using Runiq.ContextSpaces.Models.Skills;
using Runiq.ContextSpaces.Models.Sources;
using Runiq.ContextSpaces.Services;

namespace Runiq.Agents.Tests.Agents;

public sealed class AgentExecutionRuntimeTests
{
    [Fact]
    public async Task ExecuteStreamAsync_ShouldEmitSkillLoadedBeforeContextSearched_WhenSkillsExist()
    {
        var agent = new Agent(
                id: "travel-agent",
                name: "Travel Agent",
                instructions: "Plan short travel routes.",
                model: "ollama/llama3")
            .UseContextSpace("travel-docs");

        var contextSpace = new ContextSpace(
                id: "travel-docs",
                name: "Travel Documents")
            .AddSource(new ContextSpaceSource(
                id: "documents",
                name: "Travel Documents",
                kind: ContextSpaceSourceKind.Unknown));

        var runtime = new AgentExecutionRuntime(
            agents: [agent],
            openAIResponsesClient: new OpenAIResponsesClient(new HttpClient()),
            openAICompatibleClient: new OpenAICompatibleClient(new HttpClient()),
            toolInvoker: new AgentToolInvoker(new ServiceCollection().BuildServiceProvider()),
            contextSpaces: [contextSpace],
            skillDiscoveryService: new StubSkillDiscoveryService([
                new ContextSpaceSkill(
                    id: "travel-planning",
                    name: "Travel Planning Skill",
                    description: "Travel behavior instructions.",
                    version: "1.0.0",
                    tags: [],
                    instructions: "Prefer compact itineraries.",
                    sourceId: "skills",
                    relativePath: "travel-planning.md")
            ]),
            sourceSearchService: new StubSourceSearchService(
                searchedDocumentCount: 1,
                results: [
                new ContextSpaceSourceSearchResult
                {
                    SourceId = "documents",
                    SourceName = "Travel Documents",
                    RelativePath = "bursa-food.md",
                    FileName = "bursa-food.md",
                    Snippet = "Bursa has notable regional food stops.",
                    Score = 32.1
                }
            ]));

        var events = new List<AgentExecutionEvent>();

        await foreach (var executionEvent in runtime.ExecuteStreamAsync(
                           agent.Id,
                           "Plan Bursa food stops."))
        {
            events.Add(executionEvent);
        }

        var skillLoadedIndex = events.FindIndex(item => item.Kind == AgentExecutionEventKind.SkillLoaded);
        var contextSearchedIndex = events.FindIndex(item => item.Kind == AgentExecutionEventKind.ContextSearched);

        Assert.InRange(skillLoadedIndex, 0, events.Count - 1);
        Assert.InRange(contextSearchedIndex, 0, events.Count - 1);
        Assert.True(skillLoadedIndex < contextSearchedIndex);

        var skill = Assert.Single(events[skillLoadedIndex].LoadedSkills!);
        Assert.Equal("travel-planning", skill.SkillId);
        Assert.Equal("Travel Planning Skill", skill.SkillName);

        var contextSearchSummary = events[contextSearchedIndex].ContextSearchSummary;
        Assert.NotNull(contextSearchSummary);
        Assert.Equal(1, contextSearchSummary.AttachedSourceCount);
        Assert.Equal(1, contextSearchSummary.SearchedDocumentCount);
        Assert.Equal(1, contextSearchSummary.CandidateCount);
        Assert.Equal(1, contextSearchSummary.SelectedCount);
    }

    [Fact]
    public async Task ExecuteStreamAsync_ShouldSelectOnlyHighConfidenceSourceResults()
    {
        var runtime = CreateRuntimeWithSourceResults(
            searchedDocumentCount: 3,
            results: [
                CreateSearchResult("bursa-guide.md", 21.25),
                CreateSearchResult("ankara-guide.md", 2.25)
            ]);

        var contextSearchedEvent = await ExecuteAndGetContextSearchedEventAsync(
            runtime,
            "travel-agent",
            "Bursa history and food");

        var selectedResult = Assert.Single(contextSearchedEvent.SourceSearchResults!);
        Assert.Equal("bursa-guide.md", selectedResult.RelativePath);
        Assert.Equal(3, contextSearchedEvent.ContextSearchSummary!.SearchedDocumentCount);
        Assert.Equal(2, contextSearchedEvent.ContextSearchSummary.CandidateCount);
        Assert.Equal(1, contextSearchedEvent.ContextSearchSummary.SelectedCount);
    }

    [Fact]
    public async Task ExecuteStreamAsync_ShouldSelectNoSourceResults_WhenScoresAreBelowMinimumThreshold()
    {
        var runtime = CreateRuntimeWithSourceResults(
            searchedDocumentCount: 3,
            results: [
                CreateSearchResult("ankara-guide.md", 7.50),
                CreateSearchResult("bursa-guide.md", 5.25),
                CreateSearchResult("istanbul-guide.md", 5.25)
            ]);

        var contextSearchedEvent = await ExecuteAndGetContextSearchedEventAsync(
            runtime,
            "travel-agent",
            "Adana travel guide");

        Assert.Empty(contextSearchedEvent.SourceSearchResults!);
        Assert.Equal(3, contextSearchedEvent.ContextSearchSummary!.SearchedDocumentCount);
        Assert.Equal(3, contextSearchedEvent.ContextSearchSummary.CandidateCount);
        Assert.Equal(0, contextSearchedEvent.ContextSearchSummary.SelectedCount);
    }

    [Fact]
    public async Task ExecuteStreamAsync_ShouldNotSendRejectedSearchResultsToModelContext()
    {
        var runtime = CreateRuntimeWithSourceResults(
            searchedDocumentCount: 3,
            results: [
                CreateSearchResult("ankara-guide.md", 7.50),
                CreateSearchResult("bursa-guide.md", 5.25),
                CreateSearchResult("istanbul-guide.md", 5.25)
            ]);

        var contextSearchedEvent = await ExecuteAndGetContextSearchedEventAsync(
            runtime,
            "travel-agent",
            "Adana travel guide");

        Assert.Empty(contextSearchedEvent.SourceSearchResults!);
        Assert.Equal(0, contextSearchedEvent.ContextSearchSummary!.SelectedCount);
    }

    [Fact]
    public async Task ExecuteStreamAsync_ShouldSelectRealSamplePdf_WhenKemeraltıMatches()
    {
        // Bu test, gerçek sample PDF kaynağındaki güçlü Türkçe entity eşleşmesinin runtime excerpt seçimine taşındığını doğrular.
        var agent = new Agent(
                id: "travel-agent",
                name: "Travel Agent",
                instructions: "Plan short travel routes.",
                model: "ollama/llama3")
            .UseContextSpace("travel-planning");

        var sourcePath = Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "samples",
                "Runiq.ContextTravelGuide",
                "Context"));

        var contextSpace = new ContextSpace(
                id: "travel-planning",
                name: "Travel Planning")
            .AddSources(sources => sources.FromFileSystem(
                id: "travel-docs",
                name: "Travel Documents",
                path: sourcePath));

        var runtime = new AgentExecutionRuntime(
            agents: [agent],
            openAIResponsesClient: new OpenAIResponsesClient(new HttpClient()),
            openAICompatibleClient: new OpenAICompatibleClient(new HttpClient()),
            toolInvoker: new AgentToolInvoker(new ServiceCollection().BuildServiceProvider()),
            contextSpaces: [contextSpace],
            skillDiscoveryService: new StubSkillDiscoveryService([]),
            sourceSearchService: new ContextSpaceSourceSearchService(
                new ContextSpaceFileSystemSourceReader()));

        var contextSearchedEvent = await ExecuteAndGetContextSearchedEventAsync(
            runtime,
            agent.Id,
            "Kemeraltı için kısa bir gezi planı çıkar");

        var selectedResults = contextSearchedEvent.SourceSearchResults!;

        Assert.Contains(selectedResults, result =>
            result.RelativePath.Equals(
                "journey-to-history-and-culture.pdf",
                StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(selectedResults, result =>
            result.RelativePath.Contains("ankara", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(selectedResults, result =>
            result.RelativePath.Contains("bursa", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(selectedResults, result =>
            result.RelativePath.Contains("istanbul", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(6, contextSearchedEvent.ContextSearchSummary!.SearchedDocumentCount);
        Assert.Equal(selectedResults.Count, contextSearchedEvent.ContextSearchSummary.SelectedCount);
    }

    private sealed class StubSkillDiscoveryService : IContextSpaceSkillDiscoveryService
    {
        private readonly IReadOnlyList<ContextSpaceSkill> skills;

        public StubSkillDiscoveryService(IReadOnlyList<ContextSpaceSkill> skills)
        {
            this.skills = skills;
        }

        public IReadOnlyList<ContextSpaceSkill> Discover(ContextSpace contextSpace)
        {
            return skills;
        }
    }

    private sealed class StubSourceSearchService : IContextSpaceSourceSearchService
    {
        private readonly int searchedDocumentCount;
        private readonly IReadOnlyList<ContextSpaceSourceSearchResult> results;

        public StubSourceSearchService(
            int searchedDocumentCount,
            IReadOnlyList<ContextSpaceSourceSearchResult> results)
        {
            this.searchedDocumentCount = searchedDocumentCount;
            this.results = results;
        }

        public Task<IReadOnlyList<ContextSpaceSourceSearchResult>> SearchAsync(
            ContextSpace contextSpace,
            string query,
            int maxResults = 5,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ContextSpaceSourceSearchResult>>(
                results.Take(maxResults).ToArray());
        }

        public Task<ContextSpaceSourceSearchResponse> SearchWithSummaryAsync(
            ContextSpace contextSpace,
            string query,
            int maxResults = 5,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ContextSpaceSourceSearchResponse(
                SearchedDocumentCount: searchedDocumentCount,
                Results: results.Take(maxResults).ToArray()));
        }
    }

    private static AgentExecutionRuntime CreateRuntimeWithSourceResults(
        int searchedDocumentCount,
        IReadOnlyList<ContextSpaceSourceSearchResult> results)
    {
        var agent = new Agent(
                id: "travel-agent",
                name: "Travel Agent",
                instructions: "Plan short travel routes.",
                model: "ollama/llama3")
            .UseContextSpace("travel-docs");

        var contextSpace = new ContextSpace(
                id: "travel-docs",
                name: "Travel Documents")
            .AddSource(new ContextSpaceSource(
                id: "documents",
                name: "Travel Documents",
                kind: ContextSpaceSourceKind.Unknown));

        return new AgentExecutionRuntime(
            agents: [agent],
            openAIResponsesClient: new OpenAIResponsesClient(new HttpClient()),
            openAICompatibleClient: new OpenAICompatibleClient(new HttpClient()),
            toolInvoker: new AgentToolInvoker(new ServiceCollection().BuildServiceProvider()),
            contextSpaces: [contextSpace],
            skillDiscoveryService: new StubSkillDiscoveryService([]),
            sourceSearchService: new StubSourceSearchService(
                searchedDocumentCount,
                results));
    }

    private static ContextSpaceSourceSearchResult CreateSearchResult(
        string relativePath,
        double score)
    {
        return new ContextSpaceSourceSearchResult
        {
            SourceId = "documents",
            SourceName = "Travel Documents",
            RelativePath = relativePath,
            FileName = relativePath,
            Snippet = $"{relativePath} snippet.",
            Score = score
        };
    }

    private static async Task<AgentExecutionEvent> ExecuteAndGetContextSearchedEventAsync(
        AgentExecutionRuntime runtime,
        string agentId,
        string input)
    {
        await foreach (var executionEvent in runtime.ExecuteStreamAsync(agentId, input))
        {
            if (executionEvent.Kind == AgentExecutionEventKind.ContextSearched)
            {
                return executionEvent;
            }
        }

        throw new InvalidOperationException("Context searched event was not emitted.");
    }
}

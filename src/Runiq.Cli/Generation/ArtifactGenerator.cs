using Runiq.Cli.Infrastructure;
using Runiq.Cli.Models;

namespace Runiq.Cli.Generation;

public sealed class ArtifactGenerator
{
    private readonly IFileSystem _fileSystem;

    public ArtifactGenerator(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public void Generate(ProjectDefinition definition)
    {
        if (!definition.IncludeSampleCode)
        {
            return;
        }

        var apiProjectRoot = Path.Combine(
            definition.Name,
            "src",
            $"{definition.Name}.Api");

        _fileSystem.WriteAllText(
            Path.Combine(apiProjectRoot, "Agents", "TravelPlannerAgent.cs"),
            CreateTravelPlannerAgentContent(definition));

        _fileSystem.WriteAllText(
            Path.Combine(apiProjectRoot, "Agents", "BudgetAdvisorAgent.cs"),
            CreateBudgetAdvisorAgentContent(definition));

        _fileSystem.WriteAllText(
            Path.Combine(apiProjectRoot, "Tools", "WeatherTool.cs"),
            CreateWeatherToolContent(definition));

        _fileSystem.WriteAllText(
            Path.Combine(apiProjectRoot, "Tools", "TripCostTool.cs"),
            CreateTripCostToolContent(definition));

        _fileSystem.WriteAllText(
            Path.Combine(apiProjectRoot, "Prompts", "travel-planner.md"),
            CreateTravelPlannerPromptContent(definition));

        _fileSystem.WriteAllText(
            Path.Combine(apiProjectRoot, "Prompts", "budget-advisor.md"),
            CreateBudgetAdvisorPromptContent(definition));

        _fileSystem.WriteAllText(
            Path.Combine(apiProjectRoot, "Workflows", "IstanbulTripWorkflow.cs"),
            CreateWorkflowContent(definition));

        if (definition.EnableMcp)
        {
            _fileSystem.WriteAllText(
                Path.Combine(apiProjectRoot, "Mcp", "README.md"),
                CreateMcpReadmeContent(definition));
        }
    }

    private static string CreateTravelPlannerAgentContent(ProjectDefinition definition)
    {
        return $$""""
               using Runiq.Agents;
               using Runiq.Agents.Tools;
               using {{definition.Name}}.Api.Tools;

               namespace {{definition.Name}}.Api.Agents;

               public static class TravelPlannerAgent
               {
                   public static Agent Create(string? apiKey)
                   {
                       return new Agent(
                           id: "travel-planner",
                           name: "Travel Planner",
                           instructions: """
                           You are the starter travel planning assistant.

                           Prompt reference: Prompts/travel-planner.md.
                           For the sample request, plan a 2-day Istanbul trip for 3 people.
                           Check WeatherTool before suggesting the itinerary.
                           Ask Budget Advisor for cost support before the final suggestion.
                           """,
                           model: "openai/gpt-5",
                           apiKey: apiKey)
                           .AddTool<WeatherTool>();
                   }
               }
               """";
    }

    private static string CreateBudgetAdvisorAgentContent(ProjectDefinition definition)
    {
        return $$""""
               using Runiq.Agents;
               using Runiq.Agents.Tools;
               using {{definition.Name}}.Api.Tools;

               namespace {{definition.Name}}.Api.Agents;

               public static class BudgetAdvisorAgent
               {
                   public static Agent Create(string? apiKey)
                   {
                       return new Agent(
                           id: "budget-advisor",
                           name: "Budget Advisor",
                           instructions: """
                           You support the Travel Planner with simple trip cost estimates.

                           Prompt reference: Prompts/budget-advisor.md.
                           Use TripCostTool when the user asks for a trip budget or estimate.
                           Keep estimates simple and clearly label them as starter sample values.
                           """,
                           model: "openai/gpt-5",
                           apiKey: apiKey)
                           .AddTool<TripCostTool>();
                   }
               }
               """";
    }

    private static string CreateWeatherToolContent(ProjectDefinition definition)
    {
        var mcpUsingStatements = definition.EnableMcp
            ? "using System.ComponentModel;\nusing ModelContextProtocol.Server;\n"
            : string.Empty;
        var mcpTypeAttribute = definition.EnableMcp
            ? "[McpServerToolType]\n"
            : string.Empty;
        var mcpMethodAttributes = definition.EnableMcp
            ? """
                  [McpServerTool(Name = "weather.get", ReadOnly = true)]
                  [Description("Gets starter sample weather for a city.")]
              """
            : string.Empty;
        var mcpParameterAttribute = definition.EnableMcp
            ? "[Description(\"The city to check.\")] "
            : string.Empty;

        return $$"""
               {{mcpUsingStatements}}using Runiq.Agents.Tools;

               namespace {{definition.Name}}.Api.Tools;

               [RuniqTool(
                   name: "weather_get",
                   description: "Gets starter sample weather for a city.")]
               {{mcpTypeAttribute}}public sealed class WeatherTool : IRuniqTool<WeatherToolInput, WeatherToolOutput>
               {
               {{mcpMethodAttributes}}
                   public string GetWeather({{mcpParameterAttribute}}string city)
                   {
                       return $"{city} weather is mild and partly cloudy, around 18 C.";
                   }

                   public Task<WeatherToolOutput> ExecuteAsync(
                       WeatherToolInput input,
                       CancellationToken cancellationToken = default)
                   {
                       return Task.FromResult(new WeatherToolOutput(
                           City: input.City,
                           Forecast: GetWeather(input.City)));
                   }
               }

               public sealed record WeatherToolInput(string City);

               public sealed record WeatherToolOutput(
                   string City,
                   string Forecast);
               """;
    }

    private static string CreateTripCostToolContent(ProjectDefinition definition)
    {
        var mcpUsingStatements = definition.EnableMcp
            ? "using System.ComponentModel;\nusing ModelContextProtocol.Server;\n"
            : string.Empty;
        var mcpTypeAttribute = definition.EnableMcp
            ? "[McpServerToolType]\n"
            : string.Empty;
        var mcpMethodAttributes = definition.EnableMcp
            ? """
                  [McpServerTool(Name = "trip.cost.estimate", ReadOnly = true)]
                  [Description("Estimates a starter sample trip cost.")]
              """
            : string.Empty;
        var peopleDescription = definition.EnableMcp
            ? "[Description(\"Number of travelers.\")] "
            : string.Empty;
        var daysDescription = definition.EnableMcp
            ? "[Description(\"Number of trip days.\")] "
            : string.Empty;

        return $$"""
               {{mcpUsingStatements}}using Runiq.Agents.Tools;

               namespace {{definition.Name}}.Api.Tools;

               [RuniqTool(
                   name: "trip_cost_estimate",
                   description: "Estimates a simple starter trip cost.")]
               {{mcpTypeAttribute}}public sealed class TripCostTool : IRuniqTool<TripCostToolInput, TripCostToolOutput>
               {
               {{mcpMethodAttributes}}
                   public string EstimateTripCost(
                       {{peopleDescription}}int peopleCount,
                       {{daysDescription}}int days)
                   {
                       var total = peopleCount * days * 75;

                       return $"Estimated starter trip cost: {total} USD.";
                   }

                   public Task<TripCostToolOutput> ExecuteAsync(
                       TripCostToolInput input,
                       CancellationToken cancellationToken = default)
                   {
                       var total = input.PeopleCount * input.Days * 75;

                       return Task.FromResult(new TripCostToolOutput(
                           PeopleCount: input.PeopleCount,
                           Days: input.Days,
                           EstimatedTotalUsd: total,
                           Summary: EstimateTripCost(input.PeopleCount, input.Days)));
                   }
               }

               public sealed record TripCostToolInput(
                   int PeopleCount,
                   int Days);

               public sealed record TripCostToolOutput(
                   int PeopleCount,
                   int Days,
                   int EstimatedTotalUsd,
                   string Summary);
               """;
    }

    private static string CreateTravelPlannerPromptContent(ProjectDefinition definition)
    {
        return $$"""
               # Travel Planner Prompt

               You are the main travel planning assistant for {{definition.Name}}.

               Sample user request:
               "Can you suggest a 2-day trip plan in Istanbul for 3 people?"

               Guidance:
               - Start with the destination, duration, and group size.
               - Check the weather before suggesting the itinerary.
               - Keep the itinerary practical and easy to scan.
               - Ask Budget Advisor for a simple cost estimate before the final suggestion.
               """;
    }

    private static string CreateBudgetAdvisorPromptContent(ProjectDefinition definition)
    {
        return $$"""
               # Budget Advisor Prompt

               You support the travel plan with simple budget guidance.

               Guidance:
               - Use the trip cost tool for estimates.
               - The starter sample assumes 75 USD per person per day.
               - Keep the budget explanation short.
               - Clearly label estimates as sample values.
               """;
    }

    private static string CreateWorkflowContent(ProjectDefinition definition)
    {
        return $$"""
               namespace {{definition.Name}}.Api.Workflows;

               public sealed class IstanbulTripWorkflow
               {
                   public string UserRequest =>
                       "Can you suggest a 2-day trip plan in Istanbul for 3 people?";

                   public IReadOnlyList<string> Steps { get; } =
                   [
                       "TravelPlannerAgent receives the Istanbul trip request.",
                       "TravelPlannerAgent calls WeatherTool.GetWeather(\"Istanbul\").",
                       "BudgetAdvisorAgent reviews budget needs.",
                       "BudgetAdvisorAgent calls TripCostTool.EstimateTripCost(3, 2).",
                       "TravelPlannerAgent combines the itinerary, weather, and budget into a final suggestion."
                   ];

                   public string Describe()
                   {
                       // TODO: Replace this outline with a Runiq workflow registration when the
                       // generated project opts into the workflow package and runtime wiring.
                       return string.Join(Environment.NewLine, Steps);
                   }
               }
               """;
    }

    private static string CreateMcpReadmeContent(ProjectDefinition definition)
    {
        return $$"""
               # MCP Starter Tools

               When MCP is enabled, this project references `Runiq.Mcp` and maps `/mcp` in `Program.cs`.

               The starter travel tools include MCP metadata and can be exposed as:

               - `weather.get`
               - `trip.cost.estimate`

               They mirror the same small sample capabilities used by the generated agents:

               - `WeatherTool.GetWeather("Istanbul")`
               - `TripCostTool.EstimateTripCost(3, 2)`
               """;
    }
}


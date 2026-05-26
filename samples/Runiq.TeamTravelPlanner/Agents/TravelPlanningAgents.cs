using Runiq.Agents;
using Runiq.Agents.Tools;
using Runiq.TeamTravelPlanner.Tools;

namespace Runiq.TeamTravelPlanner.Agents;

/// <summary>
/// Team Travel Planner örneğinde kullanılan seyahat planlama agent tanımlarını oluşturur.
/// </summary>
public static class TravelPlanningAgents
{
    /// <summary>
    /// Hava durumu analizinden sorumlu agent tanımını oluşturur.
    /// </summary>
    public static Agent CreateWeatherAgent(string? apiKey)
    {
        return new Agent(
                id: "weather-agent",
                name: "Weather Agent",
                instructions: """
                You are the Weather Analyst in a travel planning team.

                Your responsibility:
                - Analyze weather and travel comfort only.
                - Always use WeatherTool when the request involves a city or trip plan.
                - After using WeatherTool, write a short natural-language contribution.
                - Mention temperature, condition, clothing/comfort advice, and whether outdoor walking is suitable.

                Boundaries:
                - Do not create an itinerary.
                - Do not suggest a full route.
                - Do not create time slots.
                - Do not write the final travel plan.
                - Do not print raw JSON.
                - Do not start with "Tool:", "Output:", or "Role:".

                Output style:
                - Keep it concise.
                - Write in the same language as the user.
                - Your output is only a specialist contribution for the next team member.
                """,
                model: "openai/gpt-5",
                apiKey: apiKey)
            .AddTool<WeatherTool>();
    }

    /// <summary>
    /// Seyahat bütçesi analizinden sorumlu agent tanımını oluşturur.
    /// </summary>
    public static Agent CreateBudgetAgent(string? apiKey)
    {
        return new Agent(
                id: "budget-agent",
                name: "Budget Agent",
                instructions: """
                You are the Budget Analyst in a travel planning team.

                Your responsibility:
                - Estimate practical travel costs only.
                - Always use BudgetEstimatorTool.
                - Consider city, people count, and duration if available.
                - After using BudgetEstimatorTool, write a short natural-language budget contribution.
                - Include total estimate, main cost categories, and one or two savings tips.

                Boundaries:
                - Do not create an itinerary.
                - Do not suggest a full route.
                - Do not create time slots.
                - Do not write the final travel plan.
                - Do not print raw JSON.
                - Do not start with "Tool:", "Output:", or "Role:".

                Output style:
                - Keep it concise.
                - Write in the same language as the user.
                - Your output is only a specialist contribution for the next team member.
                """,
                model: "openai/gpt-5",
                apiKey: apiKey)
            .AddTool<BudgetEstimatorTool>();
    }

    /// <summary>
    /// Gezilecek yer önerilerinden sorumlu agent tanımını oluşturur.
    /// </summary>
    public static Agent CreatePlacesAgent(string? apiKey)
    {
        return new Agent(
                id: "places-agent",
                name: "Places Agent",
                instructions: """
                You are the Places Researcher in a travel planning team.

                Your responsibility:
                - Suggest realistic and walkable places only.
                - Always use PlacesTool.
                - After using PlacesTool, write a short natural-language places contribution.
                - Mention recommended places, area grouping, walking difficulty, and route considerations.

                Boundaries:
                - Do not create a full itinerary.
                - Do not create detailed time slots.
                - Do not write the final travel plan.
                - Do not print raw JSON.
                - Do not start with "Tool:", "Output:", or "Role:".

                Output style:
                - Keep it concise.
                - Prefer practical, walkable, low-fatigue suggestions.
                - Write in the same language as the user.
                - Your output is only a specialist contribution for the final planner.
                """,
                model: "openai/gpt-5",
                apiKey: apiKey)
            .AddTool<PlacesTool>();
    }

    /// <summary>
    /// Nihai seyahat planını oluşturan agent tanımını oluşturur.
    /// </summary>
    public static Agent CreatePlannerAgent(string? apiKey)
    {
        return new Agent(
                id: "planner-agent",
                name: "Planner Agent",
                instructions: """
                You are the final Travel Planner in a travel planning team.

                Your responsibility:
                - Create the final user-facing itinerary.
                - Use the previous team member contributions as context.
                - Always use MealSuggestionTool before finalizing.
                - Synthesize weather, budget, places, route logic, meal suggestions, and user constraints.
                - Produce a practical, clear, low-fatigue travel plan.

                Final answer requirements:
                - Answer in the same language as the user.
                - Include timing, route flow, weather awareness, budget awareness, rest breaks, and meal suggestions.
                - Keep the plan realistic and not overloaded.
                - Prefer concise sections and readable bullets.
                - Call MealSuggestionTool exactly once before finalizing, unless the first tool call fails.

                Boundaries:
                - Do not print raw JSON.
                - Do not expose internal tool output directly.
                - Do not start with "Tool:", "Output:", or "Role:".
                - Do not mention that you are the final planner unless it is useful to the user.
                """,
                model: "openai/gpt-5",
                apiKey: apiKey)
            .AddTool<MealSuggestionTool>();
    }
}
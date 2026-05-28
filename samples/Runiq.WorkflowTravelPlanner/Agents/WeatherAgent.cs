using Runiq.Agents;
using Runiq.Agents.Tools;
using Runiq.WorkflowTravelPlanner.Tools;

namespace Runiq.WorkflowTravelPlanner.Agents;

/// <summary>
/// Seyahat workflow'unda hava durumu analizinden sorumlu agent tanÄ±mÄ±nÄ± iÃ§erir.
/// </summary>
public sealed class WeatherAgent : Agent
{
    private WeatherAgent(string? apiKey)
        : base(
            id: "weather-agent",
            name: "Weather Agent",
            instructions: """
            You are the Weather Analyst in a deterministic travel planning workflow.

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
            - Your output is only a specialist contribution for the next workflow step.
            """,
            model: "openai/gpt-5",
            apiKey: apiKey)
    {
    }

    /// <summary>
    /// Hava durumu agent tanÄ±mÄ±nÄ± tool baÄŸlantÄ±larÄ±yla birlikte oluÅŸturur.
    /// </summary>
    public static Agent Create(string? apiKey)
    {
        return new WeatherAgent(apiKey)
            .AddTool<WeatherTool>();
    }
}

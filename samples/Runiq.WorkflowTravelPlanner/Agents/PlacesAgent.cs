using Runiq.Agents;
using Runiq.Agents.Tools;
using Runiq.WorkflowTravelPlanner.Tools;

namespace Runiq.WorkflowTravelPlanner.Agents;

/// <summary>
/// Seyahat workflow'unda gezilecek yer önerilerinden sorumlu agent tanımını içerir.
/// </summary>
public sealed class PlacesAgent : Agent
{
    private PlacesAgent(string? apiKey)
        : base(
            id: "places-agent",
            name: "Places Agent",
            instructions: """
            You are the Places Researcher in a deterministic travel planning workflow.

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
    {
    }

    /// <summary>
    /// Gezilecek yer agent tanımını tool bağlantılarıyla birlikte oluşturur.
    /// </summary>
    public static Agent Create(string? apiKey)
    {
        return new PlacesAgent(apiKey)
            .AddTool<PlacesTool>();
    }
}

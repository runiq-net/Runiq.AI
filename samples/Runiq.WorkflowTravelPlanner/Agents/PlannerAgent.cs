using Runiq.Agents;
using Runiq.Agents.Tools;
using Runiq.WorkflowTravelPlanner.Tools;

namespace Runiq.WorkflowTravelPlanner.Agents;

/// <summary>
/// Seyahat workflow'unda nihai seyahat planını oluşturan agent tanımını içerir.
/// </summary>
public sealed class PlannerAgent : Agent
{
    private PlannerAgent(string? apiKey)
        : base(
            id: "planner-agent",
            name: "Planner Agent",
            instructions: """
            You are the final Travel Planner in a deterministic travel planning workflow.

            Your responsibility:
            - Create the final user-facing itinerary.
            - Use the previous workflow step output as context.
            - Always use MealSuggestionTool before finalizing.
            - Synthesize weather, places, route logic, meal suggestions, and user constraints.
            - Produce a practical, clear, low-fatigue travel plan.

            Final answer requirements:
            - Answer in the same language as the user.
            - Include timing, route flow, weather awareness, rest breaks, and meal suggestions.
            - Keep the plan realistic and not overloaded.
            - Prefer concise sections and readable bullets.
            - Call MealSuggestionTool exactly once before finalizing, unless the first tool call fails.

            Boundaries:
            - Do not print raw JSON.
            - Do not expose internal tool output directly.
            - Do not start with "Tool:", "Output:", or "Role:".
            """,
            model: "openai/gpt-5",
            apiKey: apiKey)
    {
    }

    /// <summary>
    /// Final planlama agent tanımını tool bağlantılarıyla birlikte oluşturur.
    /// </summary>
    public static Agent Create(string? apiKey)
    {
        return new PlannerAgent(apiKey)
            .AddTool<MealSuggestionTool>();
    }
}

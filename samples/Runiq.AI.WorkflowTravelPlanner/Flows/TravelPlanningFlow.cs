using Runiq.AI.WorkflowTravelPlanner.Agents;
using Runiq.AI.Workflows.Domain;

namespace Runiq.AI.WorkflowTravelPlanner.Flows;

/// <summary>
/// Creates the deterministic travel planning flow definition.
/// </summary>
public static class TravelPlanningFlow
{
    public static Flow Create()
    {
        return new Flow(
                id: "travel-planning-workflow",
                name: "Travel Planning Flow")
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
}


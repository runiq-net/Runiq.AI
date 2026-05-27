using Runiq.WorkflowTravelPlanner.Agents;
using Runiq.Workflows;

namespace Runiq.WorkflowTravelPlanner.Workflows;

/// <summary>
/// Seyahat planlama için deterministic agent workflow tanımını oluşturur.
/// </summary>
public static class TravelPlanningWorkflow
{
    /// <summary>
    /// Hava durumu, yer araştırması ve final planlama adımlarını sırayla çalıştıran workflow'u döndürür.
    /// </summary>
    public static Workflow Create()
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
}

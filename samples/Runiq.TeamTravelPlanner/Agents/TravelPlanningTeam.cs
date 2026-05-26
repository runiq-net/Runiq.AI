using Runiq.Teams.Models.Teams;

namespace Runiq.TeamTravelPlanner.Teams;

/// <summary>
/// Team Travel Planner örneği için sıralı çalışan agent takımını oluşturur.
/// </summary>
public static class TravelPlanningTeam
{
    /// <summary>
    /// Hava durumu, bütçe, yer ve final planlama agent'larını sırayla çalıştıran takım tanımını oluşturur.
    /// </summary>
    public static AgentTeam Create()
    {
        return new AgentTeam(
                id: "travel-planning-team",
                name: "Travel Planning Team",
                instructions: "This team creates practical travel plans by combining weather, budget, places, and final itinerary planning.")
            .UseSequentialMode()
            .AddMember(
                "weather-agent",
                "Weather Analyst",
                "Use WeatherTool and provide only weather facts plus concise natural-language travel advice. Do not print raw JSON. Do not create an itinerary.")
            .AddMember(
                "budget-agent",
                "Budget Analyst",
                "Use BudgetEstimatorTool and provide only a natural-language budget estimate plus budget advice. Do not print raw JSON. Do not create an itinerary.")
            .AddMember(
                "places-agent",
                "Places Researcher",
                "Use PlacesTool and provide only natural-language recommended places plus route considerations. Do not print raw JSON. Do not create a full itinerary.")
            .AddMember(
                "planner-agent",
                "Travel Planner",
                "Use MealSuggestionTool, synthesize all previous member outputs, and produce the final user-facing itinerary in the user's language. Do not print raw JSON. Do not start with Tool:, Output:, or Role:.");
    }

    /// <summary>
    /// Kullanıcı isteğine göre gerekli agent'ları model destekli seçen adaptif takım tanımını oluşturur.
    /// </summary>
    public static AgentTeam CreateAdaptive()
    {
        return new AgentTeam(
                id: "adaptive-travel-planning-team",
                name: "Adaptive Travel Planning Team",
                instructions: "This team adaptively selects the travel specialists needed for each request, then creates practical travel guidance.")
            .UseAdaptiveMode()
            .AddMember(
                "weather-agent",
                "Weather Analyst",
                "Use when weather, outdoor comfort, walking, or city conditions matter. Use WeatherTool and provide concise natural-language travel comfort advice.")
            .AddMember(
                "budget-agent",
                "Budget Analyst",
                "Use when cost, people count, duration, affordability, or practical budget awareness matters. Use BudgetEstimatorTool and provide concise budget guidance.")
            .AddMember(
                "places-agent",
                "Places Researcher",
                "Use when destination, historical places, route, or walking feasibility matters. Use PlacesTool and provide concise place and route findings.")
            .AddMember(
                "planner-agent",
                "Travel Planner",
                "Use for final synthesis and the final user-facing itinerary. Use MealSuggestionTool when creating a trip plan that needs meal or break suggestions.");
    }
}

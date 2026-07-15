namespace Runiq.AI.ContextTravelGuide.Services;

public sealed class TravelSummaryService : ITravelSummaryService
{
    public TravelSummaryResult CreateSummary(TravelSummaryRequest request)
    {
        var summary =
            $"{request.TravelerCount} traveler(s) can plan a {request.Days}-day trip to {request.City}. " +
            "This response was produced by an ASP.NET Core service and exposed through Runiq.AI.Mcp.";

        return new TravelSummaryResult(
            request.City,
            request.Days,
            request.TravelerCount,
            summary);
    }
}

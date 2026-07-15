namespace Runiq.AI.ContextTravelGuide.Services;

public interface ITravelSummaryService
{
    TravelSummaryResult CreateSummary(TravelSummaryRequest request);
}

public sealed record TravelSummaryRequest(
    string City,
    int Days,
    int TravelerCount);

public sealed record TravelSummaryResult(
    string City,
    int Days,
    int TravelerCount,
    string Summary);
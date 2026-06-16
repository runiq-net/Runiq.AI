using System.ComponentModel;
using ModelContextProtocol.Server;
using Runiq.ContextTravelGuide.Services;

namespace Runiq.ContextTravelGuide.McpTools;

[McpServerToolType]
public sealed class TravelSummaryMcpTool
{
    private readonly ITravelSummaryService _travelSummaryService;

    public TravelSummaryMcpTool(ITravelSummaryService travelSummaryService)
    {
        _travelSummaryService = travelSummaryService;
    }

    [McpServerTool]
    [Description("Creates a simple travel summary using an ASP.NET Core application service.")]
    public TravelSummaryResult CreateTravelSummary(
        [Description("The city to visit.")] string city,
        [Description("The number of trip days.")] int days,
        [Description("The number of travelers.")] int travelerCount)
    {
        var request = new TravelSummaryRequest(city, days, travelerCount);

        return _travelSummaryService.CreateSummary(request);
    }
}
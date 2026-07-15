using Runiq.AI.Agents.Tools;

namespace Runiq.AI.ContextTravelGuide.Tools;

[RuniqTool(
    name: "travel_budget_estimate",
    description: "Estimates a simple demo travel budget for a group based on city, group size, day count, and travel style.")]
public sealed class TravelBudgetEstimateTool : IRuniqTool<TravelBudgetEstimateInput, TravelBudgetEstimateOutput>
{
    private const string Currency = "TRY";
    private const string EstimateNote = "Demo estimate only. Accommodation, intercity transportation, museum tickets, and personal expenses are not included.";

    public Task<TravelBudgetEstimateOutput> ExecuteAsync(
        TravelBudgetEstimateInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var city = string.IsNullOrWhiteSpace(input.City)
            ? "Unknown"
            : input.City.Trim();

        var groupSize = input.GroupSize <= 0 ? 1 : input.GroupSize;
        var dayCount = input.DayCount <= 0 ? 1 : input.DayCount;
        var travelStyle = NormalizeTravelStyle(input.TravelStyle);
        var rates = GetRates(travelStyle);

        var estimatedMealCost = groupSize * dayCount * rates.MealPerPersonPerDay;
        var estimatedLocalTransportCost = groupSize * dayCount * rates.TransportPerPersonPerDay;
        var estimatedGuideOrActivityCost = groupSize * dayCount * rates.ActivityPerPersonPerDay;
        var estimatedTotalCost =
            estimatedMealCost +
            estimatedLocalTransportCost +
            estimatedGuideOrActivityCost;

        var output = new TravelBudgetEstimateOutput(
            City: city,
            GroupSize: groupSize,
            DayCount: dayCount,
            TravelStyle: travelStyle,
            EstimatedMealCost: estimatedMealCost,
            EstimatedLocalTransportCost: estimatedLocalTransportCost,
            EstimatedGuideOrActivityCost: estimatedGuideOrActivityCost,
            EstimatedTotalCost: estimatedTotalCost,
            Currency: Currency,
            Note: EstimateNote);

        return Task.FromResult(output);
    }

    private static string NormalizeTravelStyle(string? travelStyle)
    {
        var normalized = travelStyle?.Trim().ToLowerInvariant();

        return normalized switch
        {
            "budget" => "budget",
            "premium" => "premium",
            _ => "standard"
        };
    }

    private static BudgetRates GetRates(string travelStyle)
    {
        return travelStyle switch
        {
            "budget" => new BudgetRates(
                MealPerPersonPerDay: 650m,
                TransportPerPersonPerDay: 250m,
                ActivityPerPersonPerDay: 200m),

            "premium" => new BudgetRates(
                MealPerPersonPerDay: 1_700m,
                TransportPerPersonPerDay: 750m,
                ActivityPerPersonPerDay: 900m),

            _ => new BudgetRates(
                MealPerPersonPerDay: 1_000m,
                TransportPerPersonPerDay: 400m,
                ActivityPerPersonPerDay: 400m)
        };
    }

    private sealed record BudgetRates(
        decimal MealPerPersonPerDay,
        decimal TransportPerPersonPerDay,
        decimal ActivityPerPersonPerDay);
}

public sealed record TravelBudgetEstimateInput(
    string City,
    int GroupSize,
    int DayCount,
    string? TravelStyle);

public sealed record TravelBudgetEstimateOutput(
    string City,
    int GroupSize,
    int DayCount,
    string TravelStyle,
    decimal EstimatedMealCost,
    decimal EstimatedLocalTransportCost,
    decimal EstimatedGuideOrActivityCost,
    decimal EstimatedTotalCost,
    string Currency,
    string Note);


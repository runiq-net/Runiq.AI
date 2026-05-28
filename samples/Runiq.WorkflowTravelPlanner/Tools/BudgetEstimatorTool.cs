using Runiq.Agents.Tools;

namespace Runiq.WorkflowTravelPlanner.Tools;

/// <summary>
/// Seyahat planlamasÄ± iÃ§in basit ve deterministik TRY bÃ¼tÃ§e tahmini Ã¼retir.
/// </summary>
[RuniqTool(
    name: "budget_estimator",
    description: "Returns deterministic demo travel cost estimates in TRY.")]
public sealed class BudgetEstimatorTool : IRuniqTool<BudgetEstimatorInput, BudgetEstimatorOutput>
{
    /// <inheritdoc />
    public Task<BudgetEstimatorOutput> ExecuteAsync(
        BudgetEstimatorInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var peopleCount = Math.Max(input.PeopleCount ?? 2, 1);
        var durationDays = Math.Max(input.DurationDays ?? 1, 1);
        var city = string.IsNullOrWhiteSpace(input.City) ? "City" : input.City.Trim();
        var dailyBase = GetDailyBase(city);

        var food = dailyBase.Food * peopleCount * durationDays;
        var transport = dailyBase.Transport * peopleCount * durationDays;
        var tickets = dailyBase.Tickets * peopleCount * durationDays;
        var estimatedTotal = food + transport + tickets;

        var output = new BudgetEstimatorOutput(
            City: city,
            PeopleCount: peopleCount,
            DurationDays: durationDays,
            Currency: "TRY",
            Food: food,
            Transport: transport,
            Tickets: tickets,
            EstimatedTotal: estimatedTotal,
            Summary: $"{peopleCount} kiÅŸi ve {durationDays} gÃ¼n iÃ§in yaklaÅŸÄ±k demo bÃ¼tÃ§e {estimatedTotal} TRY.");

        return Task.FromResult(output);
    }

    private static DailyBudget GetDailyBase(string city)
    {
        return NormalizeCity(city) switch
        {
            "ISTANBUL" => new DailyBudget(Food: 900, Transport: 250, Tickets: 650),
            "IZMIR" => new DailyBudget(Food: 800, Transport: 220, Tickets: 450),
            "ANKARA" => new DailyBudget(Food: 750, Transport: 200, Tickets: 500),
            _ => new DailyBudget(Food: 700, Transport: 200, Tickets: 400)
        };
    }

    private static string NormalizeCity(string city)
    {
        return city
        .Trim()
        .Replace('İ', 'I')
        .Replace('ı', 'i')
        .ToUpperInvariant();
    }

    private sealed record DailyBudget(int Food, int Transport, int Tickets);
}

/// <summary>
/// BudgetEstimatorTool iÃ§in ÅŸehir, kiÅŸi sayÄ±sÄ± ve sÃ¼re bilgisini taÅŸÄ±yan girdi modelidir.
/// </summary>
public sealed record BudgetEstimatorInput(
    string City,
    int? PeopleCount,
    int? DurationDays);

/// <summary>
/// BudgetEstimatorTool tarafÄ±ndan Ã¼retilen deterministik bÃ¼tÃ§e sonucudur.
/// </summary>
public sealed record BudgetEstimatorOutput(
    string City,
    int PeopleCount,
    int DurationDays,
    string Currency,
    int Food,
    int Transport,
    int Tickets,
    int EstimatedTotal,
    string Summary);

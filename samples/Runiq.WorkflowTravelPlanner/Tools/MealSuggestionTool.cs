using Runiq.Agents.Tools;

namespace Runiq.WorkflowTravelPlanner.Tools;

/// <summary>
/// Seyahat planÄ±na uygun deterministik Ã¶ÄŸle ve akÅŸam yemeÄŸi bÃ¶lgesi Ã¶nerileri dÃ¶ndÃ¼rÃ¼r.
/// </summary>
[RuniqTool(
    name: "meal_suggestion",
    description: "Returns deterministic demo lunch and dinner area suggestions for a city.")]
public sealed class MealSuggestionTool : IRuniqTool<MealSuggestionInput, MealSuggestionOutput>
{
    /// <inheritdoc />
    public Task<MealSuggestionOutput> ExecuteAsync(
        MealSuggestionInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var city = string.IsNullOrWhiteSpace(input.City) ? "City" : input.City.Trim();
        var output = NormalizeCity(city) switch
        {
            "ISTANBUL" => new MealSuggestionOutput(
                City: city,
                LunchArea: "Sultanahmet or EminÃ¶nÃ¼",
                DinnerArea: "KarakÃ¶y or Galata",
                Notes: "Choose lunch near the historic route and finish with a relaxed dinner across the bridge."),
            "IZMIR" => new MealSuggestionOutput(
                City: city,
                LunchArea: "KemeraltÄ±",
                DinnerArea: "Kordon or Alsancak",
                Notes: "Keep lunch close to the bazaar route and end near the waterfront."),
            "ANKARA" => new MealSuggestionOutput(
                City: city,
                LunchArea: "HamamÃ¶nÃ¼",
                DinnerArea: "TunalÄ± or KÄ±zÄ±lay",
                Notes: "Use HamamÃ¶nÃ¼ for a low-pressure midday break and pick a central dinner area."),
            _ => new MealSuggestionOutput(
                City: city,
                LunchArea: "City center",
                DinnerArea: "Old town or main square",
                Notes: "Keep meals close to the walking route to avoid unnecessary transfers.")
        };

        return Task.FromResult(output);
    }

    private static string NormalizeCity(string city)
    {
        return city
              .Trim()
              .Replace('İ', 'I')
              .Replace('ı', 'i')
              .ToUpperInvariant();
    }
}

/// <summary>
/// MealSuggestionTool iÃ§in ÅŸehir bilgisini taÅŸÄ±yan girdi modelidir.
/// </summary>
public sealed record MealSuggestionInput(string City);

/// <summary>
/// MealSuggestionTool tarafÄ±ndan Ã¼retilen yemek bÃ¶lgesi Ã¶nerisi sonucudur.
/// </summary>
public sealed record MealSuggestionOutput(
    string City,
    string LunchArea,
    string DinnerArea,
    string Notes);

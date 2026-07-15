using Runiq.AI.Agents.Tools;

namespace Runiq.AI.WorkflowTravelPlanner.Tools;

/// <summary>
/// Seyahat planına uygun deterministik öğle ve akşam yemeği bölgesi önerileri döndürür.
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
                LunchArea: "Sultanahmet or Eminönü",
                DinnerArea: "Karaköy or Galata",
                Notes: "Choose lunch near the historic route and finish with a relaxed dinner across the bridge."),
            "IZMIR" => new MealSuggestionOutput(
                City: city,
                LunchArea: "Kemeraltı",
                DinnerArea: "Kordon or Alsancak",
                Notes: "Keep lunch close to the bazaar route and end near the waterfront."),
            "ANKARA" => new MealSuggestionOutput(
                City: city,
                LunchArea: "Hamamönü",
                DinnerArea: "Tunalı or Kızılay",
                Notes: "Use Hamamönü for a low-pressure midday break and pick a central dinner area."),
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
              .Replace('I', 'I')
              .Replace('i', 'i')
              .ToUpperInvariant();
    }
}

/// <summary>
/// MealSuggestionTool için şehir bilgisini taşıyan girdi modelidir.
/// </summary>
public sealed record MealSuggestionInput(string City);

/// <summary>
/// MealSuggestionTool tarafından üretilen yemek bölgesi önerisi sonucudur.
/// </summary>
public sealed record MealSuggestionOutput(
    string City,
    string LunchArea,
    string DinnerArea,
    string Notes);


using Runiq.Agents.Tools;

namespace Runiq.WorkflowTravelPlanner.Tools;

/// <summary>
/// Seyahat planlamasÄ± iÃ§in ÅŸehir bazlÄ± deterministik ve yÃ¼rÃ¼nebilir yer Ã¶nerileri dÃ¶ndÃ¼rÃ¼r.
/// </summary>
[RuniqTool(
    name: "places",
    description: "Returns deterministic demo place suggestions for a city trip.")]
public sealed class PlacesTool : IRuniqTool<PlacesInput, PlacesOutput>
{
    /// <inheritdoc />
    public Task<PlacesOutput> ExecuteAsync(
        PlacesInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var city = string.IsNullOrWhiteSpace(input.City) ? "City" : input.City.Trim();
        IReadOnlyList<PlaceSuggestion> places = NormalizeCity(city) switch
        {
            "ISTANBUL" =>
            [
                new PlaceSuggestion("Sultanahmet Square", "Historic Peninsula", 45, "Good starting point for a light historic walk."),
                new PlaceSuggestion("Hagia Sophia area", "Sultanahmet", 60, "Keep the visit focused and avoid peak queues when possible."),
                new PlaceSuggestion("GÃ¼lhane Park", "Sirkeci", 40, "Useful green break after museum-heavy stops."),
                new PlaceSuggestion("KarakÃ¶y", "BeyoÄŸlu", 50, "Works well for coffee, lunch, or ferry-side walking."),
                new PlaceSuggestion("Galata Tower area", "Galata", 45, "Best as a short scenic stop rather than a long climb-heavy route.")
            ],
            "IZMIR" =>
            [
                new PlaceSuggestion("Konak Square", "Konak", 35, "Central and easy to combine with nearby stops."),
                new PlaceSuggestion("KemeraltÄ±", "Konak", 75, "Good for food, shopping, and short covered walking breaks."),
                new PlaceSuggestion("Agora", "Namazgah", 60, "Historic stop close enough for a simple city route."),
                new PlaceSuggestion("Kadifekale", "Kadifekale", 45, "Use transport for the climb and keep time controlled."),
                new PlaceSuggestion("Kordon", "Alsancak", 60, "Relaxed waterfront finish.")
            ],
            "ANKARA" =>
            [
                new PlaceSuggestion("AnÄ±tkabir", "AnÄ±ttepe", 90, "Strong anchor stop for the day."),
                new PlaceSuggestion("Anadolu Medeniyetleri MÃ¼zesi", "Ulus", 75, "Best indoor historical stop."),
                new PlaceSuggestion("Ankara Castle", "Ulus", 60, "Scenic but plan for slopes and breaks."),
                new PlaceSuggestion("HamamÃ¶nÃ¼", "AltÄ±ndaÄŸ", 60, "Good low-pressure walking and lunch area.")
            ],
            _ =>
            [
                new PlaceSuggestion("City center", "Central area", 45, "Start with the easiest orientation point."),
                new PlaceSuggestion("Old town area", "Historic district", 60, "Keep the route compact and walkable."),
                new PlaceSuggestion("Local museum", "Museum district", 60, "Useful indoor break and cultural anchor."),
                new PlaceSuggestion("Waterfront or main square", "Public gathering area", 45, "Good relaxed finish for the route.")
            ]
        };

        return Task.FromResult(new PlacesOutput(city, places));
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
/// PlacesTool iÃ§in ÅŸehir bilgisini taÅŸÄ±yan girdi modelidir.
/// </summary>
public sealed record PlacesInput(string City);

/// <summary>
/// PlacesTool tarafÄ±ndan dÃ¶ndÃ¼rÃ¼len yer Ã¶nerileri sonucudur.
/// </summary>
public sealed record PlacesOutput(
    string City,
    IReadOnlyList<PlaceSuggestion> Places);

/// <summary>
/// Tek bir gezi duraÄŸÄ± iÃ§in pratik rota bilgisini temsil eder.
/// </summary>
public sealed record PlaceSuggestion(
    string Name,
    string Area,
    int EstimatedMinutes,
    string Note);

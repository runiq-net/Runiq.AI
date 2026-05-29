using Runiq.Agents.Tools;

namespace Runiq.WorkflowTravelPlanner.Tools;

/// <summary>
/// Seyahat planlamasÄ± iÃ§in ÅŸehir bazlÄ± deterministik hava durumu bilgisi dÃ¶ndÃ¼rÃ¼r.
/// </summary>
[RuniqTool(
    name: "weather",
    description: "Returns deterministic demo weather guidance for a city.")]
public sealed class WeatherTool : IRuniqTool<WeatherInput, WeatherOutput>
{
    /// <inheritdoc />
    public Task<WeatherOutput> ExecuteAsync(
        WeatherInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var city = NormalizeCity(input.City);
        var output = city.ToUpperInvariant() switch
        {
            "ISTANBUL" => new WeatherOutput(
                City: "Istanbul",
                TemperatureCelsius: 22,
                Condition: "Partly Cloudy",
                Advice: "Suitable for walking with a light jacket."),
            "IZMIR" => new WeatherOutput(
                City: "Izmir",
                TemperatureCelsius: 28,
                Condition: "Sunny",
                Advice: "Suitable for outdoor plans but avoid noon heat."),
            "ANKARA" => new WeatherOutput(
                City: "Ankara",
                TemperatureCelsius: 18,
                Condition: "Windy",
                Advice: "Plan indoor breaks."),
            _ => new WeatherOutput(
                City: city,
                TemperatureCelsius: 24,
                Condition: "Mild",
                Advice: "Suitable for a balanced city walk.")
        };

        return Task.FromResult(output);
    }

    private static string NormalizeCity(string city)
    {
        if (string.IsNullOrWhiteSpace(city))
        {
            return "City";
        }

        return city
            .Trim()
            .Replace('İ', 'I')
            .Replace('ı', 'i')
            .Replace('İ', 'I');
    }
}

/// <summary>
/// WeatherTool iÃ§in ÅŸehir bilgisini taÅŸÄ±yan girdi modelidir.
/// </summary>
public sealed record WeatherInput(string City);

/// <summary>
/// WeatherTool tarafÄ±ndan Ã¼retilen deterministik hava durumu sonucudur.
/// </summary>
public sealed record WeatherOutput(
    string City,
    int TemperatureCelsius,
    string Condition,
    string Advice);

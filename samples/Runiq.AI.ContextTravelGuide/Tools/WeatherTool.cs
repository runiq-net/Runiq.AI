using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Runiq.AI.Agents.Tools;

namespace Runiq.AI.ContextTravelGuide.Tools;

[RuniqTool(
    name: "weather",
    description: "Gets current weather information for a city using Open-Meteo. Use this for weather-aware travel planning, current weather, and outdoor planning questions.")]
public sealed class WeatherTool : IRuniqTool<WeatherInput, WeatherOutput>
{
    private static readonly HttpClient HttpClient = CreateHttpClient();

    /// <inheritdoc />
    public async Task<WeatherOutput> ExecuteAsync(
    WeatherInput input,
    CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (string.IsNullOrWhiteSpace(input.City))
        {
            throw new ArgumentException(
                "City is required.",
                nameof(input));
        }

        var city = input.City.Trim();

        try
        {
            var location = await FindLocationAsync(city, cancellationToken);

            if (location is null)
            {
                return Unknown(city, $"{city} için hava durumu konumu bulunamadi.");
            }

            var weather = await GetCurrentWeatherAsync(
                location.Latitude,
                location.Longitude,
                cancellationToken);

            if (weather is null)
            {
                return Unknown(location.Name, $"{location.Name} için güncel hava durumu alinamadi.");
            }

            var temperature = (int)Math.Round(weather.TemperatureCelsius);
            var condition = MapWeatherCode(weather.WeatherCode);

            return new WeatherOutput(
                City: location.Name,
                TemperatureCelsius: temperature,
                Condition: condition,
                Summary: $"{location.Name} için hava {ToTurkishCondition(condition)} ve yaklasik {temperature} derece.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return Unknown(city, $"{city} için hava durumu servisine ulasilamadi. Planlama genel hava varsayimi olmadan yapilmalidir.");
        }
    }

    private static WeatherOutput Unknown(string city, string summary)
    {
        return new WeatherOutput(
            City: city,
            TemperatureCelsius: 0,
            Condition: "Unknown",
            Summary: summary);
    }


    private static HttpClient CreateHttpClient()
    {
        var httpClient = new HttpClient();

        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Runiq.AI.ContextTravelGuide.Sample/1.0");

        return httpClient;
    }

    private static async Task<LocationResult?> FindLocationAsync(
        string city,
        CancellationToken cancellationToken)
    {
        var url =
            "https://geocoding-api.open-meteo.com/v1/search" +
            $"?name={Uri.EscapeDataString(city)}" +
            "&count=1" +
            "&language=en" +
            "&format=json";

        var response = await HttpClient.GetFromJsonAsync<GeocodingResponse>(
            url,
            cancellationToken);

        return response?.Results?.FirstOrDefault();
    }

    private static async Task<CurrentWeatherResult?> GetCurrentWeatherAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken)
    {
        var latitudeText = latitude.ToString(CultureInfo.InvariantCulture);
        var longitudeText = longitude.ToString(CultureInfo.InvariantCulture);

        var url =
            "https://api.open-meteo.com/v1/forecast" +
            $"?latitude={latitudeText}" +
            $"&longitude={longitudeText}" +
            "&current=temperature_2m,weather_code" +
            "&timezone=auto";

        var response = await HttpClient.GetFromJsonAsync<ForecastResponse>(
            url,
            cancellationToken);

        if (response?.Current is null)
        {
            return null;
        }

        return new CurrentWeatherResult(
            response.Current.TemperatureCelsius,
            response.Current.WeatherCode);
    }

    private static string MapWeatherCode(int weatherCode)
    {
        return weatherCode switch
        {
            0 => "Clear",
            1 or 2 or 3 => "Partly cloudy",
            45 or 48 => "Fog",
            51 or 53 or 55 => "Drizzle",
            56 or 57 => "Freezing drizzle",
            61 or 63 or 65 => "Rain",
            66 or 67 => "Freezing rain",
            71 or 73 or 75 => "Snow",
            77 => "Snow grains",
            80 or 81 or 82 => "Rain showers",
            85 or 86 => "Snow showers",
            95 => "Thunderstorm",
            96 or 99 => "Thunderstorm with hail",
            _ => "Unknown"
        };
    }

    private static string ToTurkishCondition(string condition)
    {
        return condition switch
        {
            "Clear" => "açik",
            "Partly cloudy" => "parçali bulutlu",
            "Fog" => "sisli",
            "Drizzle" => "çiselemeli",
            "Freezing drizzle" => "dondurucu çiselemeli",
            "Rain" => "yagmurlu",
            "Freezing rain" => "dondurucu yagmurlu",
            "Snow" => "karli",
            "Snow grains" => "kar taneli",
            "Rain showers" => "saganak yagisli",
            "Snow showers" => "kar saganakli",
            "Thunderstorm" => "gök gürültülü",
            "Thunderstorm with hail" => "dolu ve gök gürültülü",
            _ => "bilinmiyor"
        };
    }

    private sealed record GeocodingResponse(
        [property: JsonPropertyName("results")]
        IReadOnlyList<LocationResult>? Results);

    private sealed record LocationResult(
        [property: JsonPropertyName("name")]
        string Name,

        [property: JsonPropertyName("latitude")]
        double Latitude,

        [property: JsonPropertyName("longitude")]
        double Longitude);

    private sealed record ForecastResponse(
        [property: JsonPropertyName("current")]
        CurrentWeatherDto? Current);

    private sealed record CurrentWeatherDto(
        [property: JsonPropertyName("temperature_2m")]
        double TemperatureCelsius,

        [property: JsonPropertyName("weather_code")]
        int WeatherCode);

    private sealed record CurrentWeatherResult(
        double TemperatureCelsius,
        int WeatherCode);
}

/// <summary>
/// Weather tool için sehir bazli hava durumu input modelidir.
/// </summary>
public sealed record WeatherInput(
    string City);

/// <summary>
/// Weather tool çalistirildiktan sonra dönen örnek hava durumu sonucudur.
/// </summary>
public sealed record WeatherOutput(
    string City,
    int TemperatureCelsius,
    string Condition,
    string Summary);

using System.Text.Json;
using Runiq.AI.Cli.Infrastructure;
using Runiq.AI.Cli.Models;

namespace Runiq.AI.Cli.Generation;

public sealed class LaunchSettingsReader
{
    private readonly IFileSystem _fileSystem;

    public LaunchSettingsReader(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public string? GetApplicationUrl(ProjectDefinition definition)
    {
        var launchSettingsPath = Path.Combine(
            definition.Name,
            "src",
            $"{definition.Name}.Api",
            "Properties",
            "launchSettings.json");

        using var document = JsonDocument.Parse(
            _fileSystem.ReadAllText(launchSettingsPath));

        if (!document.RootElement.TryGetProperty("profiles", out var profiles))
        {
            return null;
        }

        var profileUrl = TryGetProfileUrl(profiles, "http");

        if (profileUrl is not null)
        {
            return profileUrl;
        }

        foreach (var profile in profiles.EnumerateObject())
        {
            profileUrl = TryGetApplicationUrl(profile.Value);

            if (profileUrl is not null)
            {
                return profileUrl;
            }
        }

        return null;
    }

    private static string? TryGetProfileUrl(
        JsonElement profiles,
        string profileName)
    {
        return profiles.TryGetProperty(profileName, out var profile)
            ? TryGetApplicationUrl(profile)
            : null;
    }

    private static string? TryGetApplicationUrl(JsonElement profile)
    {
        if (!profile.TryGetProperty("applicationUrl", out var applicationUrlElement))
        {
            return null;
        }

        var applicationUrl = applicationUrlElement.GetString();

        if (string.IsNullOrWhiteSpace(applicationUrl))
        {
            return null;
        }

        var urls = applicationUrl
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return urls.FirstOrDefault(url => url.StartsWith("http://localhost:", StringComparison.OrdinalIgnoreCase))
            ?? urls.FirstOrDefault(url => url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            ?? urls.FirstOrDefault();
    }
}


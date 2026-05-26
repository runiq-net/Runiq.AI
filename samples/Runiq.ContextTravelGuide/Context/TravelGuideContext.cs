using Runiq.ContextSpaces.Models.Sources;

namespace Runiq.ContextTravelGuide.Context;

/// <summary>
/// Travel planning agent'larinin kullanacagi örnek context space tanimini olusturur.
/// </summary>
internal static class TravelPlanningContext
{
    /// <summary>
    /// Travel planning agent'lari için örnek context space döner.
    /// </summary>
    public static ContextSpace Create()
    {
        return new ContextSpace(
                id: "travel-planning",
                name: "Travel Planning Context",
                description: "Shared read-only context for city trip planning agents.")
            .AddSources(sources => sources.FromFileSystem(
                id: "travel-docs",
                name: "Travel Documents",
                path: "./Context",
                description: "Sample travel planning documents and city guide notes."))
            .AddSkills(skills => skills.FromFileSystem(
                id: "travel-skills",
                name: "Travel Skills",
                path: "./Skills"));
    }
}

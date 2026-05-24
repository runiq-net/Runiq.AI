using Runiq.ContextSpaces.Models.Sources;

namespace SampleApp.Contexts;

/// <summary>
/// Travel planning agent'larının kullanacağı örnek context space tanımını oluşturur.
/// </summary>
internal static class TravelPlanningContext
{
    /// <summary>
    /// Travel planning agent'ları için örnek context space döner.
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
                path: "./Contexts/TravelPlanning/sources",
                description: "Sample travel planning documents and city guide notes."))
            .AddSkills(skills => skills.FromFileSystem(
                id: "travel-skills",
                name: "Travel Skills",
                path: "./Contexts/TravelPlanning/skills"));
    }
}
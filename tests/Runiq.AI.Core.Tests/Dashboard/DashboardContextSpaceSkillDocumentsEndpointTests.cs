using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Runiq.AI.ContextSpaces.Models.Sources;
using Runiq.AI.Core;

namespace Runiq.AI.Core.Tests.Dashboard;

/// <summary>
/// Dashboard context space skill document endpoint davranışlarını doğrulayan testleri içerir.
/// </summary>
[Collection("Dashboard assets")]
public sealed class DashboardContextSpaceSkillDocumentsEndpointTests
{
    [Fact]
    public async Task SkillDocumentsEndpoint_ShouldReturnDiscoveredSkillsGroupedBySource()
    {
        using var directory = TemporaryDirectory.Create();
        WriteSkill(directory.Path, "travel-planning", "Travel Planning");

        using var server = CreateServer(directory.Path);

        var response = await server.GetTestClient().GetAsync(
            "/dashboard/api/context-spaces/travel-planning/skill-documents");

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        Assert.Equal("travel-planning", document.RootElement.GetProperty("contextSpaceId").GetString());

        var source = Assert.Single(document.RootElement.GetProperty("skillSources").EnumerateArray());

        Assert.Equal("travel-skills", source.GetProperty("sourceId").GetString());
        Assert.Equal("Travel Skills", source.GetProperty("sourceName").GetString());
        Assert.Equal("FileSystem", source.GetProperty("provider").GetString());
        Assert.Equal(1, source.GetProperty("skillCount").GetInt32());

        var skill = Assert.Single(source.GetProperty("skills").EnumerateArray());

        Assert.Equal("travel-planning", skill.GetProperty("skillId").GetString());
        Assert.Equal("travel-planning/SKILL.md", skill.GetProperty("relativePath").GetString());
        Assert.True(skill.GetProperty("isPreviewSupported").GetBoolean());
    }

    [Fact]
    public async Task SkillDocumentPreviewEndpoint_ShouldReturnSkillMarkdownContent()
    {
        using var directory = TemporaryDirectory.Create();
        WriteSkill(directory.Path, "travel-planning", "Travel Planning");

        using var server = CreateServer(directory.Path);

        var response = await server.GetTestClient().GetAsync(
            "/dashboard/api/context-spaces/travel-planning/skill-documents/preview?skillId=travel-planning");

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        Assert.Equal("travel-planning", document.RootElement.GetProperty("contextSpaceId").GetString());
        Assert.Equal("travel-planning", document.RootElement.GetProperty("skillId").GetString());
        Assert.Equal("travel-planning/SKILL.md", document.RootElement.GetProperty("relativePath").GetString());
        Assert.Equal("text/markdown", document.RootElement.GetProperty("contentType").GetString());
        Assert.Contains("Travel Planning", document.RootElement.GetProperty("content").GetString());
        Assert.False(document.RootElement.GetProperty("isTruncated").GetBoolean());
    }

    [Fact]
    public async Task SkillDocumentPreviewEndpoint_ShouldRejectUnknownSkillId()
    {
        using var directory = TemporaryDirectory.Create();
        WriteSkill(directory.Path, "travel-planning", "Travel Planning");

        using var server = CreateServer(directory.Path);

        var response = await server.GetTestClient().GetAsync(
            "/dashboard/api/context-spaces/travel-planning/skill-documents/preview?skillId=missing");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SkillDocumentPreviewEndpoint_ShouldNotReadArbitraryPaths()
    {
        using var directory = TemporaryDirectory.Create();
        WriteSkill(directory.Path, "travel-planning", "Travel Planning");
        File.WriteAllText(Path.Combine(directory.Path, "outside.md"), "Outside content.");

        using var server = CreateServer(directory.Path);

        var response = await server.GetTestClient().GetAsync(
            "/dashboard/api/context-spaces/travel-planning/skill-documents/preview?skillId=../outside.md");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static IHost CreateServer(string skillSourcePath)
    {
        PrepareDashboardAssets();

        return new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseContentRoot(AppContext.BaseDirectory)
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddRouting();

                        services.AddRuniqServer();
                        services.AddSingleton<IReadOnlyList<ContextSpace>>(
                        [
                            new ContextSpace(
                                    id: "travel-planning",
                                    name: "Travel Planning")
                                .AddSkills(skills => skills.FromFileSystem(
                                    id: "travel-skills",
                                    name: "Travel Skills",
                                    path: skillSourcePath)),
                        ]);
                    })
                    .Configure(app =>
                    {
                        app.UseRuniqDashboard(options =>
                        {
                            options.Path = "/dashboard";
                            options.Title = "Test Dashboard";
                            options.Authentication(auth => auth.AllowAnonymous());
                        });
                    });
            })
            .Start();
    }

    private static void WriteSkill(
        string rootPath,
        string skillId,
        string title)
    {
        var skillDirectory = Path.Combine(rootPath, skillId);
        Directory.CreateDirectory(skillDirectory);

        File.WriteAllText(
            Path.Combine(skillDirectory, "SKILL.md"),
            $$"""
            ---
            name: {{skillId}}
            description: {{title}} instructions
            version: 1.0.0
            tags:
              - travel
            ---

            # {{title}}

            Use this skill during travel planning.
            """);
    }

    private static void PrepareDashboardAssets()
    {
        var root = Path.Combine(
            AppContext.BaseDirectory,
            "Studio",
            "wwwroot");

        Directory.CreateDirectory(root);

        File.WriteAllText(
            Path.Combine(root, "index.html"),
            """
            <!doctype html>
            <html>
            <head>
                <title>__RUNIQ_TITLE_HTML__</title>
                <script>
                    window.__RUNIQ_DASHBOARD__ = __RUNIQ_DASHBOARD_CONFIG__;
                </script>
            </head>
            <body>Runiq Dashboard</body>
            </html>
            """);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"runiq-skill-browser-{Guid.NewGuid():N}");

            Directory.CreateDirectory(path);

            return new TemporaryDirectory(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}


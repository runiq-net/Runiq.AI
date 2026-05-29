using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Runiq.ContextSpaces.Models.Sources;
using Runiq.Core;

namespace Runiq.Core.Tests.Dashboard;

/// <summary>
/// Dashboard context space source document endpoint davranÄ±ÅŸlarÄ±nÄ± doÄŸrulayan testleri iÃ§erir.
/// </summary>
[Collection("Dashboard assets")]
public sealed class DashboardContextSpaceSourceDocumentsEndpointTests
{
    [Fact]
    public async Task SourceDocumentsEndpoint_ShouldReturnDocumentsFromFileSystemSource()
    {
        using var directory = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(directory.Path, "ankara-guide.md"), "# Ankara");
        File.WriteAllText(Path.Combine(directory.Path, "bursa-guide.md"), "# Bursa");

        using var server = CreateServer(directory.Path);

        var response = await server.GetTestClient().GetAsync(
            "/dashboard/api/context-spaces/travel-planning/source-documents");

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        Assert.Equal("travel-planning", document.RootElement.GetProperty("contextSpaceId").GetString());

        var sourceGroup = Assert.Single(document.RootElement.GetProperty("sourceGroups").EnumerateArray());

        Assert.Equal("travel-documents", sourceGroup.GetProperty("sourceId").GetString());
        Assert.Equal("Travel Documents", sourceGroup.GetProperty("sourceName").GetString());
        Assert.Equal("FileSystem", sourceGroup.GetProperty("provider").GetString());
        Assert.Equal(2, sourceGroup.GetProperty("documentCount").GetInt32());

        var documents = sourceGroup.GetProperty("documents").EnumerateArray().ToArray();

        Assert.Equal(2, documents.Length);
        Assert.Contains(documents, item =>
            item.GetProperty("relativePath").GetString() == "ankara-guide.md" &&
            item.GetProperty("contentType").GetString() == "text/markdown" &&
            item.GetProperty("isPreviewSupported").GetBoolean());
    }

    [Theory]
    [InlineData("guide.md", "text/markdown")]
    [InlineData("notes.txt", "text/plain")]
    [InlineData("data.json", "application/json")]
    public async Task SourceDocumentPreviewEndpoint_ShouldReturnContentForSupportedFiles(
        string fileName,
        string expectedContentType)
    {
        using var directory = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(directory.Path, fileName), "Preview content.");

        using var server = CreateServer(directory.Path);

        var response = await server.GetTestClient().GetAsync(
            $"/dashboard/api/context-spaces/travel-planning/source-documents/preview?sourceId=travel-documents&path={Uri.EscapeDataString(fileName)}");

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        Assert.Equal("travel-planning", document.RootElement.GetProperty("contextSpaceId").GetString());
        Assert.Equal("travel-documents", document.RootElement.GetProperty("sourceId").GetString());
        Assert.Equal(fileName, document.RootElement.GetProperty("relativePath").GetString());
        Assert.Equal(expectedContentType, document.RootElement.GetProperty("contentType").GetString());
        Assert.Equal("Preview content.", document.RootElement.GetProperty("content").GetString());
        Assert.False(document.RootElement.GetProperty("isTruncated").GetBoolean());
    }

    [Fact]
    public async Task SourceDocumentPreviewEndpoint_ShouldRejectPathTraversal()
    {
        using var directory = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(directory.Path, "safe.md"), "Safe content.");

        using var server = CreateServer(directory.Path);

        var response = await server.GetTestClient().GetAsync(
            "/dashboard/api/context-spaces/travel-planning/source-documents/preview?sourceId=travel-documents&path=../safe.md");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SourceDocumentPreviewEndpoint_ShouldNotReadOutsideSourceRoot()
    {
        using var directory = TemporaryDirectory.Create();
        var outsidePath = Path.Combine(Path.GetDirectoryName(directory.Path)!, $"outside-{Guid.NewGuid():N}.md");
        await File.WriteAllTextAsync(outsidePath, "Outside content.");

        try
        {
            using var server = CreateServer(directory.Path);

            var response = await server.GetTestClient().GetAsync(
                $"/dashboard/api/context-spaces/travel-planning/source-documents/preview?sourceId=travel-documents&path={Uri.EscapeDataString("../" + Path.GetFileName(outsidePath))}");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally
        {
            if (File.Exists(outsidePath))
            {
                File.Delete(outsidePath);
            }
        }
    }

    [Fact]
    public async Task SourceDocumentPreviewEndpoint_ShouldRejectUnsupportedExtension()
    {
        using var directory = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(directory.Path, "image.png"), "Unsupported content.");

        using var server = CreateServer(directory.Path);

        var response = await server.GetTestClient().GetAsync(
            "/dashboard/api/context-spaces/travel-planning/source-documents/preview?sourceId=travel-documents&path=image.png");

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task SourceDocumentsEndpoint_ShouldReturnEmptyDocuments_WhenSourceDirectoryIsEmpty()
    {
        using var directory = TemporaryDirectory.Create();
        using var server = CreateServer(directory.Path);

        var response = await server.GetTestClient().GetAsync(
            "/dashboard/api/context-spaces/travel-planning/source-documents");

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        var sourceGroup = Assert.Single(document.RootElement.GetProperty("sourceGroups").EnumerateArray());

        Assert.Equal(0, sourceGroup.GetProperty("documentCount").GetInt32());
        Assert.Empty(sourceGroup.GetProperty("documents").EnumerateArray());
    }

    private static IHost CreateServer(string sourcePath)
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

                        services.AddRuniqServer(options =>
                        {
                            options.AddContextSpace(new ContextSpace(
                                    id: "travel-planning",
                                    name: "Travel Planning",
                                    description: "Shared read-only travel context.")
                                .AddSources(sources => sources.FromFileSystem(
                                    id: "travel-documents",
                                    name: "Travel Documents",
                                    path: sourcePath)));
                        });
                    })
                    .Configure(app =>
                    {
                        app.UseRuniqDashboard(options =>
                        {
                            options.Path = "/dashboard";
                            options.Title = "Test Dashboard";
                        });
                    });
            })
            .Start();
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
                $"runiq-context-browser-{Guid.NewGuid():N}");

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

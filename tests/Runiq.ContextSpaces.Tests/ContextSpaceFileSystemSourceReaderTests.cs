using Runiq.ContextSpaces.Models.Sources;
using Runiq.ContextSpaces.Services;

namespace Runiq.ContextSpaces.Tests;

public sealed class ContextSpaceFileSystemSourceReaderTests
{
    [Fact]
    public async Task ReadAsync_ShouldReadSupportedFilesRecursively()
    {
        // Bu test, dosya sistemi source'u altındaki desteklenen dokümanların recursive okunabildiğini doğrular.
        using var testDirectory = TemporaryDirectory.Create();

        File.WriteAllText(
            Path.Combine(testDirectory.Path, "overview.md"),
            "# Travel Plan");

        Directory.CreateDirectory(Path.Combine(testDirectory.Path, "policies"));

        File.WriteAllText(
            Path.Combine(testDirectory.Path, "policies", "rules.txt"),
            "Use public transport when possible.");

        File.WriteAllText(
            Path.Combine(testDirectory.Path, "settings.json"),
            """{ "city": "Ankara" }""");

        var contextSpace = new ContextSpace(
            id: "travel-planning",
            name: "Travel Planning");

        contextSpace.AddSources(sources => sources.FromFileSystem(
            id: "travel-docs",
            name: "Travel Documents",
            path: testDirectory.Path));

        var reader = new ContextSpaceFileSystemSourceReader();

        var documents = await reader.ReadAsync(contextSpace);

        Assert.Equal(3, documents.Count);

        Assert.Contains(documents, document =>
            document.RelativePath == "overview.md" &&
            document.ContentType == "text/markdown" &&
            document.Content.Contains("Travel Plan", StringComparison.Ordinal));

        Assert.Contains(documents, document =>
            document.RelativePath == "policies/rules.txt" &&
            document.ContentType == "text/plain" &&
            document.Content.Contains("public transport", StringComparison.Ordinal));

        Assert.Contains(documents, document =>
            document.RelativePath == "settings.json" &&
            document.ContentType == "application/json" &&
            document.Content.Contains("Ankara", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReadAsync_ShouldSkipUnsupportedFiles()
    {
        // Bu test, desteklenmeyen dosya uzantılarının source reader tarafından atlandığını doğrular.
        using var testDirectory = TemporaryDirectory.Create();

        File.WriteAllText(
            Path.Combine(testDirectory.Path, "notes.txt"),
            "Supported source document.");

        File.WriteAllText(
            Path.Combine(testDirectory.Path, "image.png"),
            "Unsupported binary-like file.");

        File.WriteAllText(
            Path.Combine(testDirectory.Path, "document.pdf"),
            "Unsupported pdf file for v1.");

        var contextSpace = new ContextSpace(
            id: "travel-planning",
            name: "Travel Planning");

        contextSpace.AddSources(sources => sources.FromFileSystem(
            id: "travel-docs",
            name: "Travel Documents",
            path: testDirectory.Path));

        var reader = new ContextSpaceFileSystemSourceReader();

        var documents = await reader.ReadAsync(contextSpace);

        var document = Assert.Single(documents);

        Assert.Equal("notes.txt", document.RelativePath);
        Assert.Equal("Supported source document.", document.Content);
    }

    [Fact]
    public async Task ReadAsync_ShouldSkipFilesLargerThanConfiguredLimit()
    {
        // Bu test, belirlenen maksimum dosya boyutunu aşan source dokümanlarının okunmadığını doğrular.
        using var testDirectory = TemporaryDirectory.Create();

        File.WriteAllText(
            Path.Combine(testDirectory.Path, "small.txt"),
            "small");

        File.WriteAllText(
            Path.Combine(testDirectory.Path, "large.txt"),
            new string('x', 100));

        var contextSpace = new ContextSpace(
            id: "travel-planning",
            name: "Travel Planning");

        contextSpace.AddSources(sources => sources.FromFileSystem(
            id: "travel-docs",
            name: "Travel Documents",
            path: testDirectory.Path));

        var reader = new ContextSpaceFileSystemSourceReader(maxFileSizeInBytes: 10);

        var documents = await reader.ReadAsync(contextSpace);

        var document = Assert.Single(documents);

        Assert.Equal("small.txt", document.RelativePath);
        Assert.Equal("small", document.Content);
    }

    [Fact]
    public async Task ReadAsync_ShouldIgnoreObjectStorageSources()
    {
        // Bu test, v1 reader'ın S3 metadata source'larını okumaya çalışmadığını doğrular.
        using var testDirectory = TemporaryDirectory.Create();

        File.WriteAllText(
            Path.Combine(testDirectory.Path, "notes.txt"),
            "Local document.");

        var contextSpace = new ContextSpace(
            id: "travel-planning",
            name: "Travel Planning");

        contextSpace.AddSources(sources =>
        {
            sources.FromS3(
                id: "remote-docs",
                name: "Remote Documents",
                bucketName: "runiq-contexts",
                prefix: "travel-planning/sources");

            sources.FromFileSystem(
                id: "local-docs",
                name: "Local Documents",
                path: testDirectory.Path);
        });

        var reader = new ContextSpaceFileSystemSourceReader();

        var documents = await reader.ReadAsync(contextSpace);

        var document = Assert.Single(documents);

        Assert.Equal("local-docs", document.SourceId);
        Assert.Equal("notes.txt", document.RelativePath);
    }

    [Fact]
    public async Task ReadAsync_ShouldThrow_WhenFileSystemSourcePathDoesNotExist()
    {
        // Bu test, hatalı dosya sistemi source path'inin sessizce yutulmadığını doğrular.
        var missingPath = Path.Combine(
            Path.GetTempPath(),
            $"runiq-missing-{Guid.NewGuid():N}");

        var contextSpace = new ContextSpace(
            id: "travel-planning",
            name: "Travel Planning");

        contextSpace.AddSources(sources => sources.FromFileSystem(
            id: "travel-docs",
            name: "Travel Documents",
            path: missingPath));

        var reader = new ContextSpaceFileSystemSourceReader();

        var exception = await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            reader.ReadAsync(contextSpace));

        Assert.Contains(missingPath, exception.Message, StringComparison.OrdinalIgnoreCase);
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
                $"runiq-context-source-{Guid.NewGuid():N}");

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
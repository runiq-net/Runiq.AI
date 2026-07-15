using Runiq.AI.ContextSpaces.Models.Sources;
using Runiq.AI.ContextSpaces.Services;
using System.Text;

namespace Runiq.AI.ContextSpaces.Tests;

public sealed class ContextSpaceFileSystemSourceReaderTests
{
    [Fact]
    public async Task ReadAsync_ShouldReadSupportedFilesRecursively()
    {
        // Bu test, dosya sistemi source'u altindaki desteklenen dokümanlarin recursive okunabildigini dogrular.
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

        WriteMinimalPdf(
            Path.Combine(testDirectory.Path, "izmir-guide.pdf"),
            "Izmir travel guide with Kordon, Kemeralti and Agora notes.");

        var contextSpace = new ContextSpace(
            id: "travel-planning",
            name: "Travel Planning");

        contextSpace.AddSources(sources => sources.FromFileSystem(
            id: "travel-docs",
            name: "Travel Documents",
            path: testDirectory.Path));

        var reader = new ContextSpaceFileSystemSourceReader();

        var documents = await reader.ReadAsync(contextSpace);

        Assert.Equal(4, documents.Count);

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

        Assert.Contains(documents, document =>
            document.RelativePath == "izmir-guide.pdf" &&
            document.ContentType == "application/pdf" &&
            document.Extension == ".pdf" &&
            document.Content.Contains("Izmir travel guide", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReadAsync_ShouldSkipUnsupportedFiles()
    {
        // Bu test, desteklenmeyen dosya uzantilarinin source reader tarafindan atlandigini dogrular.
        using var testDirectory = TemporaryDirectory.Create();

        File.WriteAllText(
            Path.Combine(testDirectory.Path, "notes.txt"),
            "Supported source document.");

        File.WriteAllText(
            Path.Combine(testDirectory.Path, "image.png"),
            "Unsupported binary-like file.");

        File.WriteAllText(
            Path.Combine(testDirectory.Path, "archive.zip"),
            "Unsupported archive file.");

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
        // Bu test, belirlenen maksimum dosya boyutunu asan source dokümanlarinin okunmadigini dogrular.
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
        // Bu test, v1 reader'in S3 metadata source'larini okumaya çalismadigini dogrular.
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
        // Bu test, hatali dosya sistemi source path'inin sessizce yutulmadigini dogrular.
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

    private static void WriteMinimalPdf(string filePath, string text)
    {
        var escapedText = text
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);

        var objects = new[]
        {
            "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n",
            "2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n",
            "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>\nendobj\n",
            "4 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n",
            $"5 0 obj\n<< /Length {Encoding.ASCII.GetByteCount($"BT /F1 12 Tf 72 720 Td ({escapedText}) Tj ET")} >>\nstream\nBT /F1 12 Tf 72 720 Td ({escapedText}) Tj ET\nendstream\nendobj\n"
        };

        var builder = new StringBuilder();
        builder.Append("%PDF-1.4\n");

        var offsets = new List<int> { 0 };

        foreach (var pdfObject in objects)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(builder.ToString()));
            builder.Append(pdfObject);
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(builder.ToString());

        builder.Append("xref\n");
        builder.Append($"0 {objects.Length + 1}\n");
        builder.Append("0000000000 65535 f \n");

        foreach (var offset in offsets.Skip(1))
        {
            builder.Append($"{offset:0000000000} 00000 n \n");
        }

        builder.Append("trailer\n");
        builder.Append($"<< /Size {objects.Length + 1} /Root 1 0 R >>\n");
        builder.Append("startxref\n");
        builder.Append(xrefOffset);
        builder.Append("\n%%EOF");

        File.WriteAllText(
            filePath,
            builder.ToString(),
            Encoding.ASCII);
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


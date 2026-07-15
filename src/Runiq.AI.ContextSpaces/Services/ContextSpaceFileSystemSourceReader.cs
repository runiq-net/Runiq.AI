using System.Text;
using UglyToad.PdfPig;
using Runiq.AI.ContextSpaces.Models.Sources;

namespace Runiq.AI.ContextSpaces.Services;

/// <summary>
/// Yerel dosya sistemi tabanli context source dokümanlarini okuyan servisi temsil eder.
/// </summary>
public sealed class ContextSpaceFileSystemSourceReader : IContextSpaceSourceReader
{
    private static readonly IReadOnlyDictionary<string, string> SupportedContentTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".md"] = "text/markdown",
            [".txt"] = "text/plain",
            [".json"] = "application/json",
            [".pdf"] = "application/pdf"
        };

    private readonly long maxFileSizeInBytes;

    /// <summary>
    /// Yeni bir dosya sistemi source reader örnegi olusturur.
    /// </summary>
    /// <param name="maxFileSizeInBytes">Okunmasina izin verilen maksimum dosya boyutu.</param>
    public ContextSpaceFileSystemSourceReader(long maxFileSizeInBytes = 5 * 1024 * 1024)
    {
        if (maxFileSizeInBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxFileSizeInBytes),
                maxFileSizeInBytes,
                "Maximum file size must be greater than zero.");
        }

        this.maxFileSizeInBytes = maxFileSizeInBytes;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContextSpaceSourceDocument>> ReadAsync(
        ContextSpace contextSpace,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contextSpace);

        var documents = new List<ContextSpaceSourceDocument>();

        foreach (var source in contextSpace.Sources)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (source.Kind != ContextSpaceSourceKind.LocalFileSystem)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(source.Path))
            {
                continue;
            }

            var sourceRoot = Path.GetFullPath(source.Path);

            if (!Directory.Exists(sourceRoot))
            {
                throw new DirectoryNotFoundException(
                    $"Context source path '{source.Path}' does not exist.");
            }

            foreach (var filePath in Directory.EnumerateFiles(
                         sourceRoot,
                         "*",
                         SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var extension = Path.GetExtension(filePath);

                if (!SupportedContentTypes.TryGetValue(extension, out var contentType))
                {
                    continue;
                }

                var fileInfo = new FileInfo(filePath);

                if (fileInfo.Length > maxFileSizeInBytes)
                {
                    continue;
                }

                var fullFilePath = Path.GetFullPath(filePath);

                if (!IsUnderRoot(sourceRoot, fullFilePath))
                {
                    continue;
                }

                var content = await ReadContentAsync(
                    fullFilePath,
                    extension,
                    cancellationToken);

                var relativePath = Path.GetRelativePath(sourceRoot, fullFilePath)
                    .Replace(Path.DirectorySeparatorChar, '/')
                    .Replace(Path.AltDirectorySeparatorChar, '/');

                documents.Add(new ContextSpaceSourceDocument
                {
                    SourceId = source.Id,
                    SourceName = source.Name,
                    RelativePath = relativePath,
                    FileName = Path.GetFileName(fullFilePath),
                    Extension = extension.ToLowerInvariant(),
                    ContentType = contentType,
                    Content = content,
                    SizeInBytes = fileInfo.Length
                });
            }
        }

        return documents
            .OrderBy(document => document.SourceId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(document => document.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsUnderRoot(string sourceRoot, string filePath)
    {
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(sourceRoot))
            + Path.DirectorySeparatorChar;

        var normalizedFilePath = Path.GetFullPath(filePath);

        return normalizedFilePath.StartsWith(
            normalizedRoot,
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Dosya uzantisina göre source doküman içerigini okunabilir metne dönüstürür.
    /// </summary>
    private static async Task<string> ReadContentAsync(
        string filePath,
        string extension,
        CancellationToken cancellationToken)
    {
        if (extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return ReadPdfContent(
                filePath,
                cancellationToken);
        }

        return await File.ReadAllTextAsync(
            filePath,
            cancellationToken);
    }

    /// <summary>
    /// PDF dokümanindan sayfa bazli metin içerigini çikarir.
    /// </summary>
    private static string ReadPdfContent(
        string filePath,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();

        using var document = PdfDocument.Open(filePath);

        foreach (var page in document.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (builder.Length > 0)
            {
                builder.AppendLine();
                builder.AppendLine();
            }

            builder.AppendLine($"--- Page {page.Number} ---");
            builder.AppendLine(page.Text);
        }

        return builder.ToString().Trim();
    }



}

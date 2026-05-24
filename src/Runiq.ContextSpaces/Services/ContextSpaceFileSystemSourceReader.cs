using Runiq.ContextSpaces.Models.Sources;

namespace Runiq.ContextSpaces.Services;

/// <summary>
/// Yerel dosya sistemi tabanlı context source dokümanlarını okuyan servisi temsil eder.
/// </summary>
public sealed class ContextSpaceFileSystemSourceReader : IContextSpaceSourceReader
{
    private static readonly IReadOnlyDictionary<string, string> SupportedContentTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".md"] = "text/markdown",
            [".txt"] = "text/plain",
            [".json"] = "application/json"
        };

    private readonly long maxFileSizeInBytes;

    /// <summary>
    /// Yeni bir dosya sistemi source reader örneği oluşturur.
    /// </summary>
    /// <param name="maxFileSizeInBytes">Okunmasına izin verilen maksimum dosya boyutu.</param>
    public ContextSpaceFileSystemSourceReader(long maxFileSizeInBytes = 512 * 1024)
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

                var content = await File.ReadAllTextAsync(
                    fullFilePath,
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
}